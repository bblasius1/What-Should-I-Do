using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum GameLanguageOption
{
    English,
    SimplifiedChinese
}

/// <summary>Stores the interface language and translates the game's authored English strings.</summary>
public static class GameLanguage
{
    const string Preference = "wsid.language";
    const string ChineseFontResource = "Fonts/NeighborhoodSansSC";

    static GameLanguageOption current = ReadPreference();
    static Font chineseFont;

    public static GameLanguageOption Current => current;
    public static event Action Changed;

    public static void Set(GameLanguageOption language)
    {
        if (language != GameLanguageOption.English && language != GameLanguageOption.SimplifiedChinese)
            throw new ArgumentOutOfRangeException(nameof(language));
        if (current == language)
            return;

        current = language;
        PlayerPrefs.SetInt(Preference, (int)language);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static string Text(string english)
    {
        if (current == GameLanguageOption.SimplifiedChinese && english != null &&
            Chinese.TryGetValue(english, out string translation))
            return translation;

        return english;
    }

    public static string Format(string english, params object[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, Text(english), arguments);

    /// <summary>Uses the bundled font for Chinese and for symbols such as the rating stars.</summary>
    public static Font FontFor(Font englishFont, bool forceChinese = false)
    {
        if (current == GameLanguageOption.SimplifiedChinese || forceChinese)
        {
            if (chineseFont == null)
                chineseFont = Resources.Load<Font>(ChineseFontResource);
            if (chineseFont != null)
                return chineseFont;
        }

        return englishFont;
    }

    static GameLanguageOption ReadPreference() =>
        PlayerPrefs.GetInt(Preference) == (int)GameLanguageOption.SimplifiedChinese
            ? GameLanguageOption.SimplifiedChinese
            : GameLanguageOption.English;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntimeState()
    {
        current = ReadPreference();
        chineseFont = null;
        Changed = null;
    }

    static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Audio settings and adaptive feedback
        ["Audio"] = "音频",
        ["Music volume"] = "音乐音量",
        ["Sound effects volume"] = "音效音量",
        ["Voice volume"] = "语音音量",
        ["Ambience volume"] = "环境音量",
        ["Learning progress {0}/{1}"] = "学习进度 {0}/{1}",
        ["Take your time and think about the clue."] = "别着急，仔细想想提示。",
        ["Look for the white stripes. They show where people should cross."] = "寻找斑马线的白色条纹，它们标明了安全通行的位置。",
        ["Stop at the edge, look both ways, and listen before crossing."] = "在路边停下，先左右观察并仔细听，再决定是否通行。",
        ["Wait until approaching vehicles have stopped or the road is clear."] = "等待来车停稳，或确认道路安全后再通过。",
        ["Listen to your classmate and check whether they need an adult's help."] = "仔细听同学说话，确认是否需要大人帮助。",
        ["Look for the teacher near the school office."] = "去学校办公室附近找老师。",
        ["Listen to the student's words and choose the matching incident."] = "回想同学说的话，选择对应的事件。",
        ["Choose the bench by the front gate as the location."] = "地点应该是校门口的长椅旁。",
        ["Lead the teacher along the path back to your friend."] = "沿着小路带老师回到同学身边。",
        ["Invite the classmate, then wait while they join the group."] = "邀请同学，等待他们加入游戏。",
        ["Move onto the open ring so everyone has room."] = "走到空出来的圆环，让每个人都有空间。",
        ["Choose a classmate who has not received the ball yet."] = "选择还没有接到球的同学。",

        // Welcome screen, level list, and settings
        ["What should\nI do?"] = "我该\n怎么做？",
        ["WHAT SHOULD I DO?"] = "我该怎么做？",
        ["Three games about crossing safely, getting help, and including everyone."] = "三个小游戏，练习安全过马路、找人帮忙和邀请大家一起玩。",
        ["Choose your character"] = "选择你的角色",
        ["Boy"] = "男孩",
        ["Girl"] = "女孩",
        ["Start"] = "开始",
        ["Settings"] = "设置",
        ["Camera motion"] = "镜头运动",
        ["Smooth"] = "流畅",
        ["Reduced"] = "减少",
        ["Reduced motion steadies the follow camera."] = "减少镜头运动，让跟随视角更平稳。",
        ["Display"] = "显示",
        ["Windowed"] = "窗口模式",
        ["Fullscreen"] = "全屏",
        ["Language"] = "语言",
        ["Back"] = "返回",
        ["Levels"] = "关卡",
        ["Choose a level"] = "选择关卡",
        ["Continue"] = "继续",
        ["Completed"] = "已完成",
        ["Finish this level to replay it."] = "完成这一关后，即可重玩。",
        ["Finish the previous level first."] = "请先完成上一关。",
        ["Best {0}/3"] = "最佳 {0}/3 星",
        ["Stars earned {0}/3"] = "本次获得 {0}/3 星",
        ["Play again"] = "再玩一次",
        ["Main menu"] = "主菜单",
        ["Next game"] = "下一关",
        ["Cross Safely"] = "安全过马路",
        ["Get Help at the School Gate"] = "在校门口求助",
        ["GET HELP AT THE SCHOOL GATE"] = "在校门口求助",
        ["Make Room for Everyone"] = "一起玩，人人有份",
        ["MAKE ROOM FOR EVERYONE"] = "一起玩，人人有份",
        ["Click an answer, press 1–3, or choose with a gamepad."] = "点击答案、按 1–3 键，或用手柄选择。",
        ["You crossed safely!"] = "你安全过马路了！",
        ["You reached the other sidewalk. Head into the schoolyard for the next game."] = "你已安全到达对面。下一关在学校院子里。",
        ["You got help!"] = "你找到人来帮忙了！",
        ["You found the teacher, explained what happened and where, then led them to your classmate."] = "你找到了老师，说清楚了发生的事和地点，\n还带老师找到了同学。",
        ["Everyone got a turn!"] = "每个人都轮到了！",
        ["You invited a classmate, made room, and passed the ball to everyone."] = "你邀请同学加入，给大家腾出了位置，还把球传给了每个人。",
        ["You made room for everyone and shared the ball."] = "你腾出位置，让每个人都接到了球。",

        // Crossing prompts and feedback
        ["Move with WASD, arrow keys, or a gamepad."] = "用 WASD、方向键或手柄移动。",
        ["Walk to the zebra crossing."] = "走到斑马线。",
        ["Walk to the white stripes."] = "走到斑马线。",
        ["Walk up to the curb."] = "走到路边。",
        ["Walk across the white stripes."] = "沿斑马线走到对面。",
        ["Other sidewalk"] = "对面的人行道",
        ["Zebra crossing"] = "斑马线",
        ["Stay on the sidewalk and head to the zebra crossing."] = "留在人行道上，朝斑马线走。",
        ["Use the white stripes to cross the road."] = "请沿斑马线过马路。",
        ["Traffic is still moving. Wait at the curb."] = "还有车在行驶，请在路边等候。",
        ["Traffic on this side is clear or stopped."] = "这一侧没有来车，或车辆已停下。",
        ["Looking left"] = "正在向左看",
        ["Looking right"] = "正在向右看",
        ["Look both ways."] = "看看左右两边。",
        ["Hold Q or LB to look left. Hold E or RB to look right."] = "按住 Q 或 LB 向左看，按住 E 或 RB 向右看。",
        ["Now look left."] = "现在向左看。",
        ["Hold Q or LB and check for moving traffic."] = "按住 Q 或 LB，看看左边是否有车在行驶。",
        ["Now look right."] = "现在向右看。",
        ["Hold E or RB and check for moving traffic."] = "按住 E 或 RB，看看右边是否有车在行驶。",
        ["Cross on the white stripes."] = "沿斑马线过马路。",
        ["Keep watching for traffic as you cross."] = "过马路时，继续留意来车。",
        ["Wait at the curb."] = "在路边等候。",
        ["A vehicle from the left is still moving."] = "左边还有一辆车在行驶。",
        ["A vehicle from the right is still moving."] = "右边还有一辆车在行驶。",
        ["Try another answer."] = "再试一个答案吧。",
        ["That's right!"] = "答对了！",
        ["Look both ways and listen before stepping out."] = "迈步前，看看左右两边，听听有没有来车。",
        ["CROSSING QUIZ"] = "过马路问答",
        ["You want to get to school on the other side. Where should you cross?"] = "你要去马路对面的学校。应该从哪里过马路？",
        ["Anywhere the road looks empty"] = "只要看起来没有车，哪里都可以过",
        ["On the white stripes of the zebra crossing"] = "沿着斑马线的白色条纹走",
        ["Between the parked cars"] = "从停着的车辆之间穿过去",
        ["Cars can come fast. Look for the white stripes."] = "车可能来得很快。找一找斑马线的白色条纹。",
        ["You are standing at the zebra crossing. What do you do first?"] = "你站在斑马线旁。第一步应该做什么？",
        ["Start running across"] = "马上跑到对面",
        ["Look left and right, and listen"] = "看看左右两边，听听有没有来车",
        ["Look at my phone"] = "低头看手机",
        ["Before you step out, check the road both ways."] = "迈步前，要看看左右两边的车况。",
        ["A car is coming, so you wait on the sidewalk. When is it safe to cross?"] = "有一辆车开过来，你在人行道上等着。\n什么时候可以安全过马路？",
        ["When the car has really stopped for you"] = "等车确实停下来让行",
        ["When I have counted to three"] = "数到三就可以过",
        ["When my friend crosses first"] = "朋友先过，我就跟着走",
        ["Stay on the sidewalk until the car has stopped."] = "留在人行道上，等车停稳。",

        // Schoolyard landmarks and dialogue
        ["SCHOOL"] = "学校",
        ["SCHOOL OFFICE"] = "学校办公室",
        ["A student lost their bag."] = "有位同学丢了书包。",
        ["A student tripped and fell."] = "有位同学绊倒了。",
        ["Two students were arguing."] = "有两位同学在争吵。",
        ["By the school office door."] = "在学校办公室门口。",
        ["By the courtyard planter."] = "在院子里的花盆旁边。",
        ["Beside the bench by the front gate."] = "在校门口的长椅旁边。",
        ["Wait for the teacher to reach the bench."] = "等老师走到长椅旁。",
        ["Return to the bench by the front gate."] = "回到校门口的长椅旁。",
        ["The teacher found the student."] = "老师找到同学了。",
        ["Your report helped them get here."] = "你说清楚了情况，帮老师找到了这里。",
        ["Lead the teacher back."] = "带老师回去。",
        ["Front gate bench"] = "校门口的长椅",
        ["Check on the student."] = "去看看那位同学。",
        ["Press F or gamepad X to talk."] = "按 F 键或手柄 X 键交谈。",
        ["Someone by the front gate is asking for help."] = "有人在校门口求助。",
        ["Student needs help"] = "需要帮助的同学",
        ["Find a teacher who can help."] = "找一位老师来帮忙。",
        ["Look near the school office."] = "去学校办公室附近找找。",
        ["Teacher on duty"] = "值班老师",
        ["Student by the bench"] = "长椅旁的同学",
        ["Classmate"] = "同学",
        ["I tripped and fell beside this bench by the front gate. Can you ask a teacher to come?"] = "我在校门口这条长椅旁绊倒了。\n你能请一位老师过来吗？",
        ["I'll find the teacher on duty."] = "我去找值班老师。",
        ["The teacher on duty is by the school office."] = "值班老师在学校办公室旁边。",
        ["Thanks. I'll look by the office."] = "谢谢。我去办公室附近找找。",
        ["I can help. Can you show me who needs me?"] = "我可以帮忙。你能告诉我谁需要帮助吗？",
        ["Someone near the front gate called out. I'll go check."] = "校门口附近有人在叫，我去看看。",
        ["What happened to the student?"] = "那位同学怎么了？",
        ["Think about what the student said by the bench."] = "回想一下长椅旁的同学说了什么。",
        ["Now I know what happened."] = "我知道发生什么事了。",
        ["Where did they fall?"] = "那位同学在哪里摔倒的？",
        ["Name the landmark beside the student so I know where to go."] = "说说同学旁边有什么，这样我就知道该去哪里。",
        ["The bench by the front gate. I’ll come with you."] = "在校门口的长椅旁。我跟你一起去。",

        // Playground prompts and turn-taking
        ["PLAY TOGETHER"] = "一起玩",
        ["Invite the classmate."] = "邀请这位同学。",
        ["Walk over to the classmate outside the circle."] = "走向圈外的同学。",
        ["Press F or gamepad X to invite."] = "按 F 键或手柄 X 键邀请同学。",
        ["Make room in the circle."] = "在圈里让出一个位置。",
        ["The classmate is joining the game."] = "这位同学正在加入游戏。",
        ["Step onto the open ring to start passing."] = "走到空着的圆环上，开始传球。",
        ["Open spot"] = "空位置",
        ["Face a classmate. Press F or gamepad X to pass."] = "面向一位同学，按 F 键或手柄 X 键传球。",
        ["{0} of {1} classmates have had a turn."] = "已有 {0} 位同学轮到，共 {1} 位。",
        ["Choose someone who is still waiting."] = "选一位还在等候的同学。",
        ["Ready to catch"] = "准备接球",
        ["Already had a turn"] = "已经轮到过",
        ["Waiting to join"] = "等待加入",
        ["Nice pass!"] = "传得好！",
    };
}
