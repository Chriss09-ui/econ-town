namespace EconTown
{
    /// <summary>
    /// 剧本时间线的唯一来源:解锁日、对手剧本日、强制决策日全部从这里取。
    /// 此前这些天数散布在 GameState/CompetitionConfig/WorldSettlementService/GameBootstrap 四处,
    /// 改一天数要动四个文件且容易漏(C9)。
    /// </summary>
    public static class TimelineConfig
    {
        public const int PlanProductionUnlockDay = 2;    // 解锁:计划生产(供给由玩家决定)
        public const int MaterialsUnlockDay = 3;         // 解锁:原料采购(成本管理)
        public const int EmployeesUnlockDay = 4;         // 解锁:招聘员工(边际产量)
        public const int TownEventsStartDay = 5;         // 随机小镇事件开始登场
        public const int CompetitionOpenDay = 16;        // Sunny Tea 开业(市场进入双店竞争)
        public const int CompetitionShareToastDay = 17;  // 份额提示日
        public const int RivalPriceCutDay = 18;          // 对手主动降价(替代品教学)
        public const int QualityUnlockDay = 19;          // 解锁:品质升级
        public const int MarketingUnlockDay = 20;        // 解锁:广告预算与品牌
        public const int PriceWarChoiceDay = 21;         // 强制价格战选择(收日阻塞)
        public const int StrategyLabUnlockDay = 22;      // 解锁:策略实验室
        public const int CompetitorEventStartDay = 23;   // 对手随机事件(28% 概率)开始
        public const int DefaultTotalDays = 30;          // 默认整局长度
    }
}
