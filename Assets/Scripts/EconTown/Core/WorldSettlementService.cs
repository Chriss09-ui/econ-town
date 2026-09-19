namespace EconTown
{
    /// <summary>唯一收日管线：所有系统先结算同一个 WorldDay，再一起进入下一天。</summary>
    public static class WorldSettlementService
    {
        public static bool TrySettleAndAdvance(GameState state, out int spoiled, out string blockedReason)
        {
            spoiled = 0;
            blockedReason = null;
            if (state == null)
            {
                blockedReason = "世界状态尚未初始化";
                return false;
            }

            // 终局后不得再执行任何世界结算:否则会在结束画面之外多写一期政府/宏观记录(C10)。
            if (state.phase == GamePhase.Ended)
            {
                blockedReason = "本局已结束";
                return false;
            }

            GovernmentSimulator.Ensure(state);
            ResidentEconomy.Ensure(state);
            MacroEconomySimulator.Ensure(state);
            LongTermExperienceService.Ensure(state);
            FinancialSystemService.Ensure(state);
            InternationalTradeService.Ensure(state);
            HousingService.Ensure(state);
            WorldClock.SyncDerivedPeriods(state);

            // 营业演出进行中时企业当天已掷出需求但尚未结算，
            // 此时收日会把开门日误记成歇业日、并在营业结束后再次结算——必须拦下。
            if (state.phase == GamePhase.Business)
            {
                blockedReason = "营业进行中——先完成或跳过今日营业，再推进到下一天";
                return false;
            }

            if (state.government.currentEvent != null && !state.government.currentEvent.resolved)
            {
                blockedReason = "先处理当前城市事件，关键公共决策不能被跳过";
                return false;
            }
            if (ProgressionService.RequiresPriceWarChoice(state))
            {
                blockedReason = "先完成当前章节的价格战选择，关键经营决策不能被跳过";
                return false;
            }
            if (state.economy.financialCrisisActive && !state.experience.enterpriseRecovery.active)
            {
                blockedReason = "连续 3 天资金低于 -1000，先处理财务危机再进入下一天";
                return false;
            }

            if (state.LastRecord == null || state.LastRecord.day != state.day)
            {
                // 竞争期歇业日不能只结算玩家：对手照常营业，份额/税基/次日产量计划才不断链。
                if (state.CompetitionActive) CompetitionSimulator.SettleClosedDay(state);
                else DaySimulator.SettleClosedDay(state);
            }
            // 金融合约在居民/政府/宏观账本之前结算：存款利息进居民收入，
            // 贷款利息进企业成本，而政策利率只先推动银行报价。
            FinancialSystemService.SettleContracts(state);
            // V0.9.1 住房日结(租金/欠租/在途房款):合约结算后、居民收日前——日报表能读到今日租金与供款(R1 插位)。
            HousingService.SettleDay(state);
            if (state.resident.profileCreated)
            {
                if (state.resident.history.Count == 0 ||
                    state.resident.history[state.resident.history.Count - 1].day != state.day)
                {
                    ResidentEconomy.EndDay(state);
                }
                else
                {
                    // 围猎三(#9):居民面板「完成今日生活并生成日报」可在收日前抢先写当日记录,
                    // 上面去重使命中 EndDay 被跳过——但此刻利息/租金/供款刚结算完,旧快照的
                    // interestIncome/interestExpense/savingsEnd/cashEnd 全部过期(BeginNextDay 清零
                    // 今日计数器后,这笔利息从任何日报里彻底消失)。不重发日薪,只回填终态快照。
                    ResidentState settled = state.resident;
                    ResidentDayRecord snapshot = settled.history[settled.history.Count - 1];
                    snapshot.interestIncome = settled.todayInterestIncome;
                    snapshot.interestExpense = settled.todayInterestExpense;
                    snapshot.savingsEnd = settled.savings;
                    snapshot.cashEnd = settled.cash;
                    snapshot.happinessEnd = settled.happiness;   // 复审补:欠租搬离分支在管线内改 happiness
                }
            }
            // V0.7 国际贸易撮合:晚于企业结算(产能余量已知)、早于政府周期(关税同期入账)。
            InternationalTradeService.SettleDayTrade(state);
            // V0.9.2 住房市场期步(房价/租金/CPI 住房分项,零随机):贸易后、政府周期与宏观期步前——
            // 当期宏观 CPI 即读住房引擎新值,财富效应同日进信心(R1 插位语义)。
            HousingService.AdvancePeriod(state);

            float interestTodayBeforeCycle = state.resident.todayInterestIncome;
            GovernmentSimulator.AdvanceCycle(state);
            // bug搜#4:政府付息(居民持国债份额)在居民日报封账之后才入账——EndDay/抢跑回填都
            // 写于 AdvanceCycle 之前,BeginNextDay(:108) 又会清零 todayInterestIncome,这笔利息
            // 从任何日报里缺席。照围猎三#9 回填口径:按前后差分补进当日记录的终态快照。
            if (state.resident.profileCreated &&
                state.resident.history.Count > 0 &&
                state.resident.history[state.resident.history.Count - 1].day == state.day)
            {
                float govInterestToResident = state.resident.todayInterestIncome - interestTodayBeforeCycle;
                if (govInterestToResident > 0f)
                {
                    ResidentDayRecord settledRecord = state.resident.history[state.resident.history.Count - 1];
                    settledRecord.interestIncome += govInterestToResident;
                    settledRecord.savingsEnd = state.resident.savings;   // 付息同时加了储蓄
                }
            }
            FinancialSystemService.CompleteSettlement(state);
            MacroEconomySimulator.AdvancePeriod(state);
            FiscalPolicyService.AdvanceLag(state.macro);
            // V0.7:落国际收支账并形成明日汇率(早于章节/新闻钩子,让它们读到本日贸易与汇率)。
            InternationalTradeService.CloseTradeDay(state);
            ProgressionService.OnDaySettled(state);
            LongTermExperienceService.OnDaySettled(state);

            spoiled = DaySimulator.AdvanceDay(state);
            // V0.7.1:氛围取数唯一消费点(PRD §123C)。必须晚于 AdvanceDay(世界日已推进、
            // 今日事件已预掷),一次性算好新一天全部视觉状态落缓存;UI 与渲染只读缓存。
            AtmosphereService.ResolveDaily(state);
            if (state.resident.profileCreated) ResidentEconomy.BeginNextDay(state);
            LongTermExperienceService.OnWorldDayStarted(state);
            WorldClock.SyncDerivedPeriods(state);
            return true;
        }
    }
}
