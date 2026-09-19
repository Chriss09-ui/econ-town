using System;
using System.Collections.Generic;

namespace EconTown
{
    /// <summary>《小镇大事记》过场的序号(PRD §148;V0.9.5 增第六段)。</summary>
    public enum CutsceneId
    {
        None = 0,
        SunnyTeaOpening = 1,   // 第 6 章清晨,开业介绍弹窗之前
        PriceWarErupts = 2,    // 第 9 章首次价格战面板之前
        CrisisClouds = 3,      // 首次企业财务危机,危机面板之前
        TradeWarHeadline = 4,  // 贸易战待决标记首次成立的次日清晨
        Finale = 5,            // 第 58 章完成,第八篇阶段报告之前(V0.9.5 自 53 迁入,§197.7)
        HousingMania = 6,      // V0.9.5 楼市狂热:镇房价首次进入 Inflating 相位的次日清晨

        // V0.9.17 篇间晋升线:五段「身份授予」过场,各挂在对应篇阶段报告之前(授予→成绩单)。
        // 1–6 的 int 值已随存档落盘绝不重排;大事记的编年展示序由 ChronicleOrder 数组解错位。
        MoveInTown = 7,          // 完成 10 章次晨(二→三篇):搬进小镇,创建居民档案之前
        CouncilAppointment = 8,  // 完成 16 章次晨(三→四篇):镇议会的聘书,进市政府之前
        BureauHandover = 9,      // 完成 22 章次晨(四→五篇):数据局的钥匙,宏观仪表盘之前
        BankMandate = 10,        // 完成 30 章次晨(五→六篇):老行长的委任状,金融中心开放之前
        Homecoming = 11          // 完成 53 章次晨(七→八篇):一纸房契,安家篇之前
    }

    /// <summary>看过即写入的过场档案:回看时引用首播当日的真实数字(PRD §150/§151)。</summary>
    [Serializable]
    public class CutsceneRecord
    {
        public int id;
        public int worldDay;
        public string caption = "";
    }
}
