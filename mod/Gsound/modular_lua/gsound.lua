local SELF = "Self"
local TARGET = "Target"
local MAIN_TARGET = "MainTarget"
local VICTIM = "Victim"

local LOVER = "Lover"
local HATER = "Hater"
local REUNION = "Reunion"
local FAREWELL = "Farewell"
local MIND_HEART = "MindHeart"
local HOPE = "Hope"
local UNFORGETTABLE_MEMORY = "UnforgettableMemory"
local SELECTIVE_AMNESIA = "SelectiveAmnesia"
local EXPLICATOR = "Explicator"
local FIRE_POKER = "FirePoker"
local BREATH = "Breath"
local BULLET = "AccelBullet"
local SLASH_UP = "SlashResultUp"
local BLANK_DOMAIN = "BlankDomain"
local CURSED_SPIRIT = "CursedSpirit"

local SKILL_ONE = 1079701
local SKILL_TWO = 1079702
local SKILL_THREE = 1079703
local FUTURE = 1079704
local PAST = 1079705
local PRESENT = 1079706
local FUTURE_ATTACK = 1079711
local PAST_ATTACK = 1079712
local WHITE_PAPER = 1079713
local ROOFTOP = 1079714
local BLANK_SKILL_ONE = 1079721
local BLANK_SKILL_TWO = 1079722
local BLANK_SKILL_THREE = 1079723
local BLANK_COUNTER = 1079724
-- Hidden COUNTER definitions used when the defense slot branches into the
-- Middle-style Envy reaction. They intentionally keep COUNTER semantics;
-- changing a defense action directly to the dashboard ATTACK IDs would make
-- the action lose its defense-phase targeting/type.
local BLANK_COUNTER_TWO = 1079725
local BLANK_COUNTER_THREE = 1079726

local D_STATE = 3201
local D_LOVER_LOST = 3202
local D_HATER_LOST = 3203
local D_INCOMING_ACTIVE = 3204
local D_FIRST_STAGGER = 3205
local D_RECOVER_STAGGER = 3206
local D_S1_REUSE = 3207
local D_S2_REUSE = 3208
local D_FUTURE_SENT = 3209
local D_PRESENT_USED = 3210
local D_REACTION_DUEL = 3212
local D_REACTION_TARGET = 3213
local D_MEMORY_USED = 3214
local D_PAST_SENT = 3215
local D_DUAL_BLADE_NEXT = 3216
local D_WHITE_PAPER_READY = 3217
local D_WHITE_PAPER_GRANTED = 3218
local D_FIRST_EMPTY_RELOAD_USED = 3219
local D_WHITE_PAPER_LAST_ROUND = 3220
local D_PAST_ATTACK_USED = 3221
local D_DUAL_CONVERT_PAIRS = 3222
local D_CLASHED_THIS_SKILL = 3223
local D_WHITE_PAPER_EXPLICATOR = 3224
local D_WHITE_PAPER_FIRE_POKER = 3225
local D_WHITE_PAPER_ACTIVE = 3226
local D_BLOOD_FIELD_CONTRIBUTION = 3227
local D_FIRE_FIELD_CONTRIBUTION = 3228
local D_WHITE_PAPER_BGM_UNTIL_ROUND = 3229
local D_AMNESIA_LEVEL_APPLIED = 3230
local D_AMNESIA_SKILL_HEALED = 3231
local D_FIRE_POKER_DARK_FLAME_COUNT = 3232
local D_BLANK_QUEUED = 3233
local D_BLANK_USED_WHITE = 3234
local D_BLANK_USED_ROOFTOP = 3235
local D_BLANK_ENTERED = 3236
local D_BLANK_S2_REUSE = 3237
local D_BLANK_S3_SPENT = 3238
local D_BLANK_S3_REUSE = 3239
local D_BLANK_COUNTER_REUSE = 3240
local D_BLANK_SKILL_HEAL = 3241
local D_BLANK_COUNTERS_THIS_ROUND = 3242
local D_BLANK_INCOMING_DAMAGE = 3243
local D_BLANK_COUNTER_SKILL = 3244
local D_BLANK_GUARD_EQUIPPED = 3245
local D_BLANK_COUNTER_VARIANT = 3246

local function clamp(value, minimum, maximum)
    if value < minimum then return minimum end
    if value > maximum then return maximum end
    return value
end

local function safe_number(value)
    if type(value) == "number" then return value end
    return 0
end

local function safe_call(fn, ...)
    if type(fn) ~= "function" then return nil end
    if type(pcall) ~= "function" then return fn(...) end
    local ok, result = pcall(fn, ...)
    if ok then return result end
    return nil
end

local function potency(unit, keyword)
    -- Victim/Target are valid data selectors in several passive timings, but
    -- Modular may not expose them to the buff acquirer in those same timings.
    -- Never let a stage-isolation check abort the legacy reaction callback.
    return safe_number(safe_call(getbuff, unit, keyword, "stack"))
end

local function count(unit, keyword)
    return safe_number(safe_call(getbuff, unit, keyword, "turn"))
end

local function total(unit, keyword)
    return safe_number(safe_call(getbuff, unit, keyword, "+"))
end

local function combat_state(unit)
    -- The visible form buffs are authoritative. D_STATE is only a fallback
    -- for the brief transition where neither form buff is present.
    if potency(unit, HATER) > 0 then return 1 end
    if potency(unit, LOVER) > 0 then return 0 end
    return safe_number(getdata(unit, D_STATE)) == 1 and 1 or 0
end

local function try_call(fn, ...)
    if type(fn) ~= "function" then return false end
    if type(pcall) ~= "function" then
        fn(...)
        return true
    end
    local ok = pcall(fn, ...)
    return ok
end

-- 过去/天台不再借用额外人物特效。拇指罗佳的处置环只属于白纸。
local function set_rooftop_aura(active, unit)
    -- intentionally empty
end

local function set_white_paper_fog(active, unit)
    try_call(gsoundpreyaura, unit or SELF, active and 1 or 0)
end

local function start_white_paper_bgm(unit)
    -- Disabled: Shattered Dream BGM is dedicated to Phase 2 (Blank Domain)
end

local function stop_white_paper_bgm(unit)
    -- The BGM consequence can invalidate Modular's temporary target selector
    -- in this timing. Persist the state before calling into the bridge and do
    -- not let a missing selector abort the rest of a form transition.
    local target = unit or SELF
    if target ~= nil then
        safe_call(setdata, target, D_WHITE_PAPER_BGM_UNTIL_ROUND, 0)
    end
    if not is_blank_domain(target) then
        try_call(gsoundbgm, "stop")
    end
end

-- Bloodfeast and Scorchfield are official StageBuffs (top-left battlefield UI).
-- They operate independently from dual blades.
local function refresh_blade_fields()
    -- No-op: Dual blades no longer forcefully modify BloodDinner/FireField stacks.
end

local function current_skill_id()
    return safe_number(safe_call(getskillid))
end

-- Counter selection and guard equipment are captured by the C# bridge only
-- after command confirmation, using native total Envy perfect resonance.
-- Do not read or latch resonance while the player is previewing a guard.

-- AfterSlots on this Modular build does not inject getdata or custom
-- acquirers. A Lua-side latch survives that restricted environment so the
-- dashboard replacement can still run on the transform round.
local BLANK_DOMAIN_LATCHED = false

local function is_blank_domain(unit)
    if BLANK_DOMAIN_LATCHED then return true end
    unit = unit or SELF
    if potency(unit, BLANK_DOMAIN) > 0 then
        BLANK_DOMAIN_LATCHED = true
        return true
    end
    if type(getgsoundblankstate) == "function"
        and safe_number(safe_call(getgsoundblankstate, unit)) == 1 then
        BLANK_DOMAIN_LATCHED = true
        return true
    end
    if type(getdata) == "function"
        and safe_number(safe_call(getdata, unit, D_BLANK_ENTERED)) == 1 then
        BLANK_DOMAIN_LATCHED = true
        return true
    end
    return false
end

local function apply_all_abnormalities(unit, stack, turn)
    buff(unit, "Laceration", stack, turn, 0)
    buff(unit, "Combustion", stack, turn, 0)
    buff(unit, "Sinking", stack, turn, 0)
    buff(unit, "Vibration", stack, turn, 0)
    buff(unit, "Burst", stack, turn, 0)
end

local function gain_cursed_spirit(amount)
    local current = potency(SELF, CURSED_SPIRIT)
    local gained = math.min(math.max(0, amount), math.max(0, 100 - current))
    if gained > 0 then buff(SELF, CURSED_SPIRIT, gained, 0, 0) end
end

local function has_special_vibration(unit)
    local keys = {
        "VibrationNesting", "VibrationContinue", "VibrationEcho",
        "VibrationChain", "VibrationAssimilation", "VibrationCrack",
        "VibrationBleeding", "VibrationCollapse", "VibrationIgnition",
        "VibrationSpring"
    }
    for _, keyword in ipairs(keys) do
        if total(unit, keyword) > 0 then return true end
    end
    return false
end

-- The removed relocation prototype left two old EndSkill callbacks behind.
-- Keep them harmless instead of letting an undefined function abort Lua.
local function check_relocation_unlock(unit)
    return false
end

local function check_white_paper_unlock(unit)
    unit = unit or "Self"
    if is_blank_domain(unit) then return end
    local ready = safe_number(safe_call(getdata, unit, 3217))
    if ready == 1 then return end

    local current_round = safe_number(safe_call(getround))
    local last_round = safe_number(safe_call(getdata, unit, 3220))
    if current_round > 0 and current_round < last_round + 3 then return end

    local explicator = safe_number(safe_call(getbuff, unit, "Explicator", "stack"))
    local fire_poker = safe_number(safe_call(getbuff, unit, "FirePoker", "stack"))
    if explicator + fire_poker >= 9 and math.min(explicator, fire_poker) >= 3 then
        safe_call(setdata, unit, 3217, 1)
        safe_call(setdata, unit, 3218, 0)
        -- BGM 也不在这里切：等白纸实际进槽的回合（gsound_round_start）再 start
        -- 不在这里亮特效：白纸要下回合才进行动槽（gsound_round_start 插入时再亮）
        local bullets = safe_number(safe_call(getbuff, unit, "AccelBullet", "stack"))
        if bullets < 10 then
            safe_call(buff, unit, "AccelBullet", 10 - bullets, 0, 0)
        end
        log("GuaHeath: White Paper unlocked")
    end
end

local function ensure_mind_heart_fx(unit)
    if potency(unit, MIND_HEART) <= 0 then return end
    -- Drop the old T Corp Borrowed Time visual donor. MindHeart now uses the
    -- official Thumb Father Rodion 心-不光彩 overlay via ShinEffect.
    local white_paper_on = safe_number(getdata(unit, D_WHITE_PAPER_GRANTED)) == 1
        or safe_number(getdata(unit, D_WHITE_PAPER_ACTIVE)) == 1
    local leftover = safe_number(getbuff(unit, "TimeRentalTwoPersonality", "turn"))
    if leftover > 0 and not white_paper_on then
        buff(unit, "TimeRentalTwoPersonality", 0, -leftover, 0)
    end
    try_call(gsoundrefreshfx, unit)
end

local function mark_mind_heart(unit)
    if potency(unit, MIND_HEART) <= 0 then
        buff(unit, MIND_HEART, 1, 0, 1)
    end
    ensure_mind_heart_fx(unit)
end

local function spend_limited(unit, keyword, amount, data_id, limit)
    local already = safe_number(getdata(unit, data_id))
    local allowed = clamp(limit - already, 0, limit)
    local spent = math.min(amount, allowed, potency(unit, keyword))
    if spent > 0 then
        buff(unit, keyword, -spent, 0, 0)
        setdata(unit, data_id, already + spent)
        if keyword == LOVER and potency(unit, LOVER) <= 0 then
            mark_mind_heart(unit)
        end
    end
    return spent
end

local function spend_lover(unit, amount)
    return spend_limited(unit, LOVER, amount, D_LOVER_LOST, 10)
end

local function spend_hater(unit, amount)
    return spend_limited(unit, HATER, amount, D_HATER_LOST, 4)
end

local function spend_reaction_cost(unit, lover_amount, hater_amount)
    if combat_state(unit) ~= 1 then
        local already = safe_number(getdata(unit, D_LOVER_LOST))
        local spent = math.min(lover_amount, clamp(10 - already, 0, 10))
        if spent > 0 then
            buff(unit, LOVER, -spent, 0, 0)
            setdata(unit, D_LOVER_LOST, already + spent)
        end
    else
        local already = safe_number(getdata(unit, D_HATER_LOST))
        local spent = math.min(hater_amount, clamp(4 - already, 0, 4))
        if spent > 0 then
            buff(unit, HATER, -spent, 0, 0)
            setdata(unit, D_HATER_LOST, already + spent)
        end
    end
end

function gsound_bloodfeast_init()
    -- Hidden field initialization now lives on the visible Dual Blades passive,
    -- so no empty BloodDinner passive card is created in the identity UI.
    try_call(gsoundstagefield, "activate")
    try_call(gsoundstagefield, "blooddinner", "ensure", 0)
    try_call(gsoundstagefield, "blooddinner", "mapfx", 0)
end

function gsound_lover_init()
    -- Clear stale visual state when restarting or re-entering an encounter.
    set_rooftop_aura(false)
    set_white_paper_fog(false)
    try_call(gsoundbgm, "stop")
    -- 确保进战时激活独立血宴伤害监听
    try_call(gsoundstagefield, "activate")
    -- 注册并确保官方战场环境 Buff (血宴与灼热地带) 显示在左上角 UI
    try_call(gsoundstagefield, "blooddinner", "ensure", 0)
    try_call(gsoundstagefield, "firefield", "ensure", 0)
    if potency(SELF, LOVER) <= 0 and potency(SELF, HATER) <= 0 then
        buff(SELF, LOVER, 20, 0, 0)
    end
    setdata(SELF, D_STATE, potency(SELF, HATER) > 0 and 1 or 0)
    setdata(SELF, D_LOVER_LOST, 0)
    setdata(SELF, D_HATER_LOST, 0)
    setdata(SELF, D_INCOMING_ACTIVE, 0)
    setdata(SELF, D_FIRST_STAGGER, 0)
    setdata(SELF, D_RECOVER_STAGGER, 0)
    setdata(SELF, D_REACTION_DUEL, 0)
    setdata(SELF, D_REACTION_TARGET, 0)
    setdata(SELF, D_MEMORY_USED, 0)
    setdata(SELF, D_PAST_SENT, 0)
    setdata(SELF, D_DUAL_BLADE_NEXT, 0)
    setdata(SELF, D_WHITE_PAPER_READY, 0)
    setdata(SELF, D_WHITE_PAPER_GRANTED, 0)
    setdata(SELF, D_WHITE_PAPER_LAST_ROUND, 0)
    setdata(SELF, D_FIRST_EMPTY_RELOAD_USED, 0)
    setdata(SELF, D_PAST_ATTACK_USED, 0)
    setdata(SELF, D_WHITE_PAPER_EXPLICATOR, 0)
    setdata(SELF, D_WHITE_PAPER_FIRE_POKER, 0)
    setdata(SELF, D_WHITE_PAPER_ACTIVE, 0)
    setdata(SELF, D_WHITE_PAPER_BGM_UNTIL_ROUND, 0)
end

function gsound_round_start()
    -- The blank passive runs later in the RoundStart passive order. When the
    -- form was queued last round, do not insert White Paper/BGM moments before
    -- gsound_blank_round_start commits the transition; that leftover slot was
    -- being converted into an unintended extra Blank S3 by AfterSlots.
    if is_blank_domain(SELF) or safe_number(getdata(SELF, D_BLANK_QUEUED)) == 1 then
        set_white_paper_fog(false)
        setdata(SELF, D_WHITE_PAPER_READY, 0)
        setdata(SELF, D_WHITE_PAPER_GRANTED, 0)
        setdata(SELF, D_WHITE_PAPER_ACTIVE, 0)
        setdata(SELF, D_DUAL_CONVERT_PAIRS, 0)
        if is_blank_domain(SELF) then
            try_call(gsoundbgm, "start")
        end
        return
    end
    setdata(SELF, D_STATE, combat_state(SELF))
    ensure_mind_heart_fx(SELF)
    setdata(SELF, D_LOVER_LOST, 0)
    setdata(SELF, D_HATER_LOST, 0)
    setdata(SELF, D_INCOMING_ACTIVE, 0)
    setdata(SELF, D_REACTION_DUEL, 0)
    setdata(SELF, D_REACTION_TARGET, 0)
    setdata(SELF, D_PAST_SENT, 0)
    setdata(SELF, D_PAST_ATTACK_USED, 0)
    setdata(SELF, D_DUAL_CONVERT_PAIRS, 0)
    setdata(SELF, D_CLASHED_THIS_SKILL, 0)
    local bgm_until_round = safe_number(getdata(SELF, D_WHITE_PAPER_BGM_UNTIL_ROUND))
    local current_round = safe_number(safe_call(getround))
    if bgm_until_round > 0 and current_round > 0 and current_round >= bgm_until_round then
        stop_white_paper_bgm()
    end
    if safe_number(getdata(SELF, D_WHITE_PAPER_READY)) == 1 then
        local white_paper_skill = 1079713
        insertskill(white_paper_skill, "bottom", 0)
        setdata(SELF, D_WHITE_PAPER_GRANTED, 1)
        set_white_paper_fog(true)
        local bullets = safe_number(getbuff(SELF, "AccelBullet", "stack"))
        if bullets < 10 then
            buff(SELF, "AccelBullet", 10 - bullets, 0, 0)
        end
        log("GuaHeath: White Paper " .. white_paper_skill .. " refreshed in leftmost bottom dashboard slot")
    end
    -- 确保战斗 UI 始终注册有官方 StageBuff
    try_call(gsoundstagefield, "blooddinner", "ensure", 0)
    try_call(gsoundstagefield, "firefield", "ensure", 0)
end

function gsound_capture_reaction_target()
    -- EnemyStartBehaviour exposes Victim to data operations but not reliably
    -- to the buff acquirer. The permanent form flag is sufficient here.
    if safe_number(safe_call(getdata, VICTIM, D_BLANK_ENTERED)) == 1 then return end
    -- EnemyStartBehaviour uses the enemy attacker as Self and the passive
    -- owner being attacked as Victim. Preserve the exact attacker because a
    -- cut-in defense action does not retain it as Future's MainTarget.
    local attacker = safe_number(safe_call(getinstid, SELF))
    if attacker > 0 then
        setdata(VICTIM, D_REACTION_TARGET, attacker)
        -- EnemyStartBehaviour fires once per enemy skill. Keep a small active
        -- count so multiple skills targeting this unit each settle once at
        -- their own EnemyEndSkill, even when their action windows overlap.
        setdata(VICTIM, D_FUTURE_SENT, 0)
        setdata(VICTIM, D_INCOMING_ACTIVE, safe_number(getdata(VICTIM, D_INCOMING_ACTIVE)) + 1)
        local reaction_skill = combat_state(VICTIM) == 1 and PAST or FUTURE
        skillsend(VICTIM, VICTIM, reaction_skill, "def", "first")
        log("GuaHeath: captured reaction target inst" .. attacker)
    end
end

function gsound_clash()
    if is_blank_domain(SELF) then return end
    local id = current_skill_id()
    if id == FUTURE or id == PAST or id == 1079706 then return end
    setdata(SELF, D_CLASHED_THIS_SKILL, 1)

    if potency(SELF, LOVER) > 0 then
        buff(SELF, REUNION, 1, 0, 0)
        spend_lover(SELF, 1)
    elseif potency(SELF, HATER) > 0 then
        local before = potency(SELF, FAREWELL)
        if before < 3 then
            buff(SELF, FAREWELL, 1, 0, 0)
            buff(SELF, "Protection", 1, 0, 0)
            log("GuaHeath: gained Farewell from clash")
            if before == 2 then
                buff(SELF, "DefenseUp", 3, 0, 0)
            end
        end
    end
end

function gsound_clear_clash_buffs()
    if is_blank_domain(SELF) then return end
    local id = current_skill_id()
    if id == FUTURE or id == PAST or id == 1079706 then
        setdata(SELF, D_CLASHED_THIS_SKILL, 0)
        return
    end
    if safe_number(getdata(SELF, D_CLASHED_THIS_SKILL)) == 1 then
        gsound_dual_blade_convert()
    end
    setdata(SELF, D_CLASHED_THIS_SKILL, 0)
    local reunion = potency(SELF, REUNION)
    if reunion > 0 then buff(SELF, REUNION, -reunion, 0, 0) end
end

function gsound_clash_lost()
    if is_blank_domain(SELF) then return end
    -- Deduct the duel-loss cost difference immediately on clash loss
    -- (Lover: 3 + 2 = 5, Hater: 2 + 1 = 3)
    spend_reaction_cost(SELF, 2, 1)
    setdata(SELF, D_REACTION_DUEL, 0)
    log("Gsound: clash lost; deducted reaction duel penalty")
end

function gsound_before_hit()
    -- BeforeWhenHit has the same selector limitation as EnemyStartBehaviour.
    if safe_number(safe_call(getdata, TARGET, D_BLANK_ENTERED)) == 1 then return end
    -- Modular BWH semantics: Self is the attacker, Target is the unit hit.
    -- Capture it here as well as at EnemyStartBehaviour. Abnormality attacks
    -- can enter a queued counter before EnemyStartBehaviour has populated the
    -- target, while BeforeWhenHit still exposes the exact attacker reliably.
    local attacker = safe_number(safe_call(getinstid, SELF))
    if attacker > 0 then
        setdata(TARGET, D_REACTION_TARGET, attacker)
        if safe_number(getdata(TARGET, D_INCOMING_ACTIVE)) <= 0 then
            setdata(TARGET, D_FUTURE_SENT, 0)
            setdata(TARGET, D_INCOMING_ACTIVE, 1)
            local reaction_skill = combat_state(TARGET) == 1 and PAST or FUTURE
            skillsend(TARGET, TARGET, reaction_skill, "def", "first")
        end
        log("GuaHeath: captured reaction target before hit inst" .. attacker)
    end
end

function gsound_on_break()
    if is_blank_domain(SELF) then return end
    if safe_number(getdata(SELF, D_FIRST_STAGGER)) == 0 then
        setdata(SELF, D_FIRST_STAGGER, 1)
        setdata(SELF, D_RECOVER_STAGGER, 1)
        mark_mind_heart(SELF)
    end
end

function gsound_lover_round_end()
    if is_blank_domain(SELF) then return end
    local state = combat_state(SELF)
    setdata(SELF, D_STATE, state)
    local stagger_state = safe_number(safe_call(getunitstate, SELF, "stagger"))

    -- OnBreak is an unreliable Modular timing, so EndBattle also performs
    -- the authoritative first-stagger check. Forced Stagger returns 2.
    if stagger_state == 1 and safe_number(getdata(SELF, D_FIRST_STAGGER)) == 0 then
        setdata(SELF, D_FIRST_STAGGER, 1)
        setdata(SELF, D_RECOVER_STAGGER, 1)
        mark_mind_heart(SELF)
    end

    if safe_number(getdata(SELF, D_RECOVER_STAGGER)) == 1 then
        if stagger_state == 1 then
            safe_call(breakrecover, SELF)
        end
        setdata(SELF, D_RECOVER_STAGGER, 0)
    end

    if potency(SELF, HATER) > 0 and safe_number(getdata(SELF, D_PAST_ATTACK_USED)) ~= 1 then
        local spent = math.min(2, potency(SELF, HATER))
        if spent > 0 then
            buff(SELF, HATER, -spent, 0, 0)
            log("GuaHeath: unused 没有时间了……, Hater -" .. spent)
        end
        state = combat_state(SELF)
        setdata(SELF, D_STATE, state)
    end

    if state == 0 and potency(SELF, LOVER) <= 0 then
        mark_mind_heart(SELF)
        buff(SELF, HATER, 10, 0, 0)
        local bullets = safe_number(getbuff(SELF, "AccelBullet", "stack"))
        if bullets < 10 then
            buff(SELF, "AccelBullet", 10 - bullets, 0, 0)
        end
        setdata(SELF, D_STATE, 1)
    elseif state == 1 and potency(SELF, HATER) <= 0 then
        buff(SELF, LOVER, 20, 0, 0)
        local bullets = safe_number(getbuff(SELF, "AccelBullet", "stack"))
        if bullets < 10 then
            buff(SELF, "AccelBullet", 10 - bullets, 0, 0)
        end
        setdata(SELF, D_STATE, 0)
    end

    -- Farewell now lasts through all actions in the current battle phase and
    -- is removed only when that phase ends.
    local farewell = potency(SELF, FAREWELL)
    if farewell > 0 then
        buff(SELF, FAREWELL, -farewell, 0, 0)
    end
end

local function gain_dual_blade()
    if safe_number(getdata(SELF, D_DUAL_BLADE_NEXT)) == 0 then
        buff(SELF, EXPLICATOR, 1, 0, 0)
        setdata(SELF, D_DUAL_BLADE_NEXT, 1)
    else
        buff(SELF, FIRE_POKER, 1, 0, 0)
        setdata(SELF, D_DUAL_BLADE_NEXT, 0)
    end
    check_white_paper_unlock()
end

function gsound_dual_blade_gain()
    if is_blank_domain(SELF) then return end
    -- Keep this callback literal-only. Modular's event VM can discard nested
    -- helper upvalues here; that previously passed Nil to getbuff while White
    -- Paper was resolving and could leave the action sequence waiting.
    if safe_number(getdata(SELF, 3226)) == 1 then return end
    local id = safe_number(safe_call(getskillid))
    if id == 1079713 then return end
    if safe_number(safe_call(onusebufiskeyword, "AccelBullet", "mainandsub")) == 1 then
        gain_dual_blade()
    end
end

function gsound_dual_blade_convert()
    if is_blank_domain(SELF) then return end
    -- White Paper's unlock is latched as soon as the threshold is reached.
    -- The triggering skill still converts its paired blades into Breath when
    -- it ends; otherwise this effect appears to vanish on the unlock action.
    if safe_number(getdata(SELF, 3226)) == 1 then return end
    local used = safe_number(getdata(SELF, 3222))
    local room = 3 - used
    if room <= 0 then return end
    -- Literal keywords: Modular can drop nested string upvalues here.
    local explicator = safe_number(getbuff(SELF, "Explicator", "stack"))
    local fire_poker = safe_number(getbuff(SELF, "FirePoker", "stack"))
    local pairs = math.min(room, explicator, fire_poker)
    if pairs > 0 then
        buff(SELF, "Explicator", -pairs, 0, 0)
        buff(SELF, "FirePoker", -pairs, 0, 0)
        buff(SELF, "Breath", pairs * 5, pairs * 5, 0)
        setdata(SELF, 3222, used + pairs)
        log("GuaHeath: converted " .. pairs .. " dual-blade pair(s) to Breath")
    end
end

function gsound_fire_poker_round_start()
    setdata(SELF, D_FIRE_POKER_DARK_FLAME_COUNT, 0)
end

function gsound_fire_poker_dark_flame()
    if is_blank_domain(SELF) then return end
    if potency(SELF, FIRE_POKER) <= 0 then return end

    -- Only the three dashboard base attack skills qualify. Follow-up attacks,
    -- defensive reactions, White Paper and Rooftop must not trigger this.
    local id = current_skill_id()
    if id ~= SKILL_ONE and id ~= SKILL_TWO and id ~= 1079703 then return end
    if potency(TARGET, "Combustion") < 30 then return end

    local used = safe_number(getdata(SELF, D_FIRE_POKER_DARK_FLAME_COUNT))
    if used >= 2 then return end

    buff(TARGET, "DarkFlame", 1, 0, 0)
    setdata(SELF, D_FIRE_POKER_DARK_FLAME_COUNT, used + 1)
end

function gsound_start_battle_attack()
    if is_blank_domain(SELF) then return end
    skillsend(SELF, "RandomEnemyNoCores99", SKILL_ONE, "atk", "first")
end

function gsound_hope_end()
    if is_blank_domain(SELF) then return end
    local overflow = clamp(potency(SELF, BREATH) - 25, 0, 99)
    local room = clamp(5 - potency(SELF, HOPE), 0, 5)
    local converted = math.min(math.floor(overflow / 15), 1, room)
    if converted > 0 then
        buff(SELF, BREATH, -(converted * 15), 0, 0)
        buff(SELF, HOPE, converted, 0, 0)
    end
end

function gsound_hope_start()
    if is_blank_domain(SELF) then return end
    local current_hope = potency(SELF, HOPE)
    local amount = clamp(current_hope, 0, 2)
    if amount > 0 then
        buff(SELF, SLASH_UP, amount, 0, 0)
    end
end

function gsound_memory_check()
    if safe_number(getdata(SELF, D_MEMORY_USED)) == 1 then
        return
    end

    local hp_ratio = safe_call(gethp, SELF, "%")
    if type(hp_ratio) ~= "number" then
        return
    end

    if hp_ratio <= 20 then
        buff(SELF, UNFORGETTABLE_MEMORY, 3, 0, 0)
        setdata(SELF, D_MEMORY_USED, 1)
    end
end

function gsound_memory_round_end()
    local memory = potency(SELF, UNFORGETTABLE_MEMORY)
    if memory <= 0 then return end

    if memory <= 1 then
        buff(SELF, UNFORGETTABLE_MEMORY, -memory, 0, 0)
        buff(SELF, SELECTIVE_AMNESIA, 1, 0, 0)
        setdata(SELF, D_AMNESIA_LEVEL_APPLIED, 0)
        setdata(SELF, D_AMNESIA_SKILL_HEALED, 0)
        log("GuaHeath: Unforgettable Memory ended -> Selective Amnesia")
    else
        buff(SELF, UNFORGETTABLE_MEMORY, -1, 0, 0)
    end
end

local function adjust_selective_amnesia_levels()
    if potency(SELF, SELECTIVE_AMNESIA) <= 0 then
        setdata(SELF, D_AMNESIA_LEVEL_APPLIED, 0)
        return
    end

    local hp_ratio = safe_call(gethp, SELF, "%")
    if type(hp_ratio) ~= "number" then return end

    local desired = clamp(math.floor((100 - hp_ratio) / 20), 0, 5)
    local applied = clamp(safe_number(getdata(SELF, D_AMNESIA_LEVEL_APPLIED)), 0, 5)
    local delta = desired - applied
    if delta ~= 0 then
        buff(SELF, "AttackUp", delta, 0, 0)
        buff(SELF, "DefenseUp", delta, 0, 0)
        setdata(SELF, D_AMNESIA_LEVEL_APPLIED, desired)
    end
end

function gsound_selective_amnesia_round_start()
    -- AttackUp and DefenseUp are round buffs, so rebuild only this effect's
    -- contribution at the start of each round.
    setdata(SELF, D_AMNESIA_LEVEL_APPLIED, 0)
    adjust_selective_amnesia_levels()
end

function gsound_selective_amnesia_hp_changed()
    adjust_selective_amnesia_levels()
end

function gsound_selective_amnesia_skill_start()
    if potency(SELF, SELECTIVE_AMNESIA) > 0 then
        setdata(SELF, D_AMNESIA_SKILL_HEALED, 0)
    end
end

function gsound_selective_amnesia_hit()
    if potency(SELF, SELECTIVE_AMNESIA) <= 0 then return end

    local already_healed = clamp(safe_number(getdata(SELF, D_AMNESIA_SKILL_HEALED)), 0, 25)
    local room = 25 - already_healed
    if room <= 0 then return end

    -- gethpdmg is Modular's actual HP-damage value for this hit; shield-only
    -- damage therefore does not create healing.
    local hp_damage = safe_number(safe_call(gethpdmg))
    local amount = math.min(room, math.floor(hp_damage * 0.5))
    if amount > 0 then
        safe_call(healhp, SELF, amount)
        setdata(SELF, D_AMNESIA_SKILL_HEALED, already_healed + amount)
    end
end

function gsound_resource_passive_start()
    if is_blank_domain(SELF) then return end
    local bullets = potency(SELF, BULLET)
    if bullets < 10 then
        buff(SELF, BULLET, 10 - bullets, 0, 0)
    end
    buff(SELF, EXPLICATOR, 1, 0, 0)
    buff(SELF, FIRE_POKER, 1, 0, 0)
    buff(SELF, BREATH, 3, 2, 0)
    buff(SELF, SLASH_UP, 2, 0, 0)
    check_white_paper_unlock(SELF)
end

function gsound_support_passive_start()
    -- Support passives do not have an on-field Self model.  EveryCoreAlly is
    -- resolved from the supporter's faction and safely reaches every active
    -- ally in both human and abnormality battles.
    buff("EveryCoreAlly", BREATH, 5, 5, 0)
    buff("EveryCoreAlly", SLASH_UP, 2, 0, 0)
    buff("EveryCoreAlly", "AttackDmgUp", 3, 0, 0)
    buff("EveryCoreAlly", "Protection", 2, 0, 0)
end

function gsound_s1_power()
    if total(SELF, BREATH) >= 5 then final(1) end
    local status = potency(TARGET, "Laceration")
        + potency(TARGET, "Combustion")
        + potency(TARGET, "Sinking")
    scale(clamp(math.floor(status / 5), 0, 3))
end

function gsound_s1_start()
    local blood = safe_number(getgsoundstagefield("blooddinner"))
    if blood >= 25 then
        gsoundstagefield("blooddinner", "sub", 25, SELF)
        buff(SELF, "BloodArmorPersonalityFirst", 1, 0, 0)
        log("GuaHeath: spent 25 BloodDinner -> 1 Hardblood (had " .. blood .. ")")
    else
        buff(SELF, "Laceration", 5, 0, 0)
        log("GuaHeath: BloodDinner consume failed have=" .. blood .. " need=25")
    end
    setdata(SELF, D_S1_REUSE, 0)
end

function gsound_s1_last_coin()
    if safe_number(getdata(SELF, D_S1_REUSE)) ~= 0 then return end
    if total(TARGET, "Vibration") >= 5 then
        setdata(SELF, D_S1_REUSE, 1)
        log("Gsound: S1 reusing final coin")
        reusecoin(-1)
    end
end

function gsound_s2_start()
    local blood = safe_number(getgsoundstagefield("blooddinner"))
    if blood >= 40 then
        gsoundstagefield("blooddinner", "sub", 40, SELF)
        buff(SELF, "BloodArmorPersonalityFirst", 1, 0, 0)
        log("GuaHeath: spent 40 BloodDinner -> 1 Hardblood (had " .. blood .. ")")
    else
        buff(SELF, "Laceration", 5, 0, 0)
        log("GuaHeath: BloodDinner consume failed have=" .. blood .. " need=40")
    end
    setdata(SELF, D_S2_REUSE, 0)
    if potency(SELF, BULLET) > 0 then
        buff(SELF, BULLET, -1, 0, 0)
        gain_dual_blade()
    end
end

function gsound_s2_reroll()
    -- Do not require issameunit(MainTarget): that check was silently failing
    -- and made S2 look like the reuse was deleted.
    local used = safe_number(getdata(SELF, 3208))
    if used >= 3 then return end
    local negatives = safe_number(safe_call(getbuffcount, TARGET, "neg"))
    local chance = 40 + negatives * 20
    if chance > 100 then chance = 100 end
    local roll = safe_number(safe_call(random, 1, 100))
    if roll <= 0 then roll = 1 end
    if roll <= chance then
        setdata(SELF, 3208, used + 1)
        log("GuaHeath: S2 reusing coin " .. (used + 1) .. " chance " .. chance)
        reusecoin(-1)
    end
end

function gsound_s3_start()
    local blood = safe_number(getgsoundstagefield("blooddinner"))
    if blood >= 50 then
        gsoundstagefield("blooddinner", "sub", 50, SELF)
        buff(SELF, "BloodArmorPersonalityFirst", 1, 0, 0)
        log("GuaHeath: spent 50 BloodDinner -> 1 Hardblood (had " .. blood .. ")")
    else
        buff(SELF, "Laceration", 5, 0, 0)
        log("GuaHeath: BloodDinner consume failed have=" .. blood .. " need=50")
    end
end

function gsound_s3_power()
    clash(clamp(math.floor(total(SELF, BREATH) / 5), 0, 3))
    final(clamp(math.floor(total(TARGET, "Combustion") / 5), 0, 3))
    base(clamp(math.floor(total(TARGET, "Laceration") / 5), 0, 3))
    scale(clamp(math.floor(total(TARGET, "Sinking") / 5), 0, 3))
end

function gsound_s3_coin4_start()
    buff(SELF, "Charge", 10, 10, 0)
end

function gsound_s3_coin5_start()
    local charge = potency(SELF, "Charge")
    if charge > 0 then
        final(charge)
        buff(SELF, "Charge", -charge, 0, 0)
    end
end

function gsound_s3_coin5_burst()
    local total_burst = total(TARGET, "Burst")
    local count = math.min(math.floor(total_burst / 5), 5)
    if count > 0 then
        for i = 1, count do
            burst(TARGET, 1)
            buff(TARGET, "Vibration", 0, -1, 0)
        end
        log("Gsound: S3 coin 5 triggered " .. count .. " dynamic Vibration Explosions")
    end
end

function gsound_future_power()
    scale(math.floor(potency(SELF, LOVER) / 2))
    base(math.max(0, math.floor((potency(SELF, LOVER) - 10) / 2)))
end

function gsound_future_start()
    -- Charge the passive-trigger cost once per enemy skill. EnemyStartBehaviour
    -- has already opened the window and reset the result to 0.
    if safe_number(getdata(SELF, D_FUTURE_SENT)) == 0
        and safe_number(getdata(SELF, D_INCOMING_ACTIVE)) > 0 then
        local duel_loss = safe_number(getdata(SELF, D_REACTION_DUEL)) == 1
        spend_reaction_cost(SELF, duel_loss and 5 or 3, duel_loss and 3 or 2)
        setdata(SELF, D_REACTION_DUEL, 0)
    end

end

function gsound_future_success()
    -- Never let a later successful coin overwrite an earlier failed evade.
    if safe_number(getdata(SELF, D_FUTURE_SENT)) ~= -1 then
        setdata(SELF, D_FUTURE_SENT, 1)
    end
end

function gsound_future_fail()
    setdata(SELF, D_FUTURE_SENT, -1)
end

function gsound_future_end()
    -- EnemyEndSkill is the per-skill settlement point. This EndSkill can run
    -- once per evade coin, so intentionally do nothing here to avoid charging
    -- Lover or spawning the follow-up once per coin.
end

function gsound_finish_future_reaction()
    local active = safe_number(safe_call(getdata, VICTIM, D_INCOMING_ACTIVE))
    if active <= 0 then return end

    if safe_number(safe_call(getdata, VICTIM, D_FUTURE_SENT)) == 1 then
        local attacker = safe_number(safe_call(getinstid, SELF))
        if attacker <= 0 then
            attacker = safe_number(safe_call(getdata, VICTIM, D_REACTION_TARGET))
        end
        local target_selector = "RandomEnemyNoCores99"
        if attacker > 0 then
            target_selector = "inst" .. attacker .. "+RandomEnemyNoCores99"
            log("GuaHeath: enemy skill ended; sending one Future follow-up to inst" .. attacker)
        else
            log("GuaHeath: enemy skill ended without attacker; using enemy fallback")
        end
        safe_call(skillsend, VICTIM, target_selector, FUTURE_ATTACK, "atk", "first")
        safe_call(buff, VICTIM, EXPLICATOR, 1, 0, 0)

        check_white_paper_unlock(VICTIM)
    end

    if combat_state(VICTIM) == 0 then
        spend_lover(VICTIM, 2)
    end
    safe_call(setdata, VICTIM, D_FUTURE_SENT, 0)
    safe_call(setdata, VICTIM, D_REACTION_TARGET, 0)
    safe_call(setdata, VICTIM, D_INCOMING_ACTIVE, math.max(0, active - 1))
end

function gsound_past_start()
    if potency(SELF, HATER) <= 0 then return end
    if safe_number(getdata(SELF, D_PAST_SENT)) == 0 then
        local duel_loss = safe_number(getdata(SELF, D_REACTION_DUEL)) == 1
        spend_reaction_cost(SELF, duel_loss and 5 or 3, duel_loss and 3 or 2)
        setdata(SELF, D_REACTION_DUEL, 0)
        log("GuaHeath: Past actually started")
    end
    local unbreakable_count = math.min(math.floor(potency(SELF, HATER) / 3), 3)
    if unbreakable_count >= 1 then makeunbreakable(0) end
    if unbreakable_count >= 2 then makeunbreakable(1) end
    if unbreakable_count >= 3 then makeunbreakable(2) end
    -- A triggered counter may retain its attacker as MainTarget even when the
    -- passive-level EnemyStartBehaviour callback has not run yet.
    local self_inst = safe_number(safe_call(getinstid, SELF))
    local current_target = safe_number(safe_call(getinstid, MAIN_TARGET))
    if current_target > 0 and current_target ~= self_inst then
        setdata(SELF, D_REACTION_TARGET, current_target)
        log("GuaHeath: Past captured MainTarget inst" .. current_target)
    end
    -- The attacker is captured at EnemyStartBehaviour, which occurs after
    -- this counter's BeforeAttack timing. Defer the follow-up until EndSkill.
    setdata(SELF, D_PAST_SENT, 1)
end

function gsound_past_end()
    if safe_number(getdata(SELF, D_PAST_SENT)) == 1 then
        local attacker = safe_number(getdata(SELF, D_REACTION_TARGET))
        local target_selector = "RandomEnemyNoCores99"
        if attacker > 0 then
            target_selector = "inst" .. attacker .. "+RandomEnemyNoCores99"
            log("GuaHeath: sending one Past follow-up to inst" .. attacker)
        else
            log("GuaHeath: Past attacker missing; using enemy fallback")
        end
        skillsend(SELF, target_selector, PAST_ATTACK, "atk", "first")
        buff(SELF, FIRE_POKER, 1, 0, 0)
        check_white_paper_unlock()
    end
    setdata(SELF, D_PAST_SENT, 0)
    setdata(SELF, D_REACTION_TARGET, 0)
    local active = safe_number(getdata(SELF, D_INCOMING_ACTIVE))
    if active > 0 then
        setdata(SELF, D_INCOMING_ACTIVE, active - 1)
    end
end

function gsound_past_lose()
    final(5)
end

function gsound_present_start()
    setdata(SELF, D_PRESENT_USED, 1)
    buff(SELF, "DefenseUp", 5, 0, 0)
end

function gsound_present_win()
    log("GuaHeath: Present won parrying")
    burst(TARGET, 1)
    buff(TARGET, "Vibration", 0, -1, 0)
    buff(SELF, EXPLICATOR, 1, 0, 0)
    buff(SELF, FIRE_POKER, 1, 0, 0)
    check_white_paper_unlock()
end

function gsound_present_end()
    local now = potency(SELF, BULLET)
    if now < 5 then
        buff(SELF, BULLET, 5 - now, 0, 0)
    end
end

function gsound_turn_end_reload()
    if is_blank_domain(SELF) then return end
    -- The first empty magazine is checked directly at round end. The old
    -- pending flag was written from OnUseBuf and was not stable across event
    -- contexts.
    local first_reload_used = getdata(SELF, 3219)
    local bullets = safe_number(getbuff(SELF, "AccelBullet", "stack"))
    if first_reload_used ~= 1 and bullets <= 0 then
        buff(SELF, "AccelBullet", 10, 0, 0)
        setdata(SELF, 3219, 1)
        safe_call(changemotion, 48)
        log("GuaHeath: first empty magazine reloaded to 10 at round end")
    end

    if safe_number(getdata(SELF, D_PRESENT_USED)) == 1 then
        -- Keep the arguments literal here. Modular's EndBattle dispatcher can
        -- lose helper upvalues and was passing Nil into getbuff through the
        -- generic reload helper.
        local now = safe_number(getbuff(SELF, "AccelBullet", "stack"))
        if now < 5 then
            buff(SELF, "AccelBullet", 5 - now, 0, 0)
            safe_call(changemotion, 48)
        end
        setdata(SELF, D_PRESENT_USED, 0)
    end

end

function gsound_followup_future_start()
    local blood = safe_number(getgsoundstagefield("blooddinner"))
    if blood >= 25 then
        gsoundstagefield("blooddinner", "sub", 25, SELF)
        buff(SELF, "BloodArmorPersonalityFirst", 1, 0, 0)
        log("GuaHeath: spent 25 BloodDinner -> 1 Hardblood (had " .. blood .. ")")
    else
        buff(SELF, "Laceration", 5, 0, 0)
        log("GuaHeath: BloodDinner consume failed have=" .. blood .. " need=25")
    end
end

function gsound_followup_future_power()
    if potency(SELF, BREATH) >= 5 then final(1) end
    local status = potency(TARGET, "Combustion") + potency(TARGET, "Vibration")
    base(clamp(math.floor(status / 5), 0, 3))
end

function gsound_followup_past_start()
    local blood = safe_number(getgsoundstagefield("blooddinner"))
    if blood >= 40 then
        gsoundstagefield("blooddinner", "sub", 40, SELF)
        buff(SELF, "BloodArmorPersonalityFirst", 1, 0, 0)
        log("GuaHeath: spent 40 BloodDinner -> 1 Hardblood (had " .. blood .. ")")
    else
        buff(SELF, "Laceration", 5, 0, 0)
        log("GuaHeath: BloodDinner consume failed have=" .. blood .. " need=40")
    end
end

function gsound_followup_past_power()
    if potency(SELF, BREATH) >= 5 then scale(1) end
    local status = potency(TARGET, "Laceration") + potency(TARGET, "Vibration")
    final(clamp(math.floor(status / 5), 0, 3))
end

function gsound_mark_past_attack()
    setdata(SELF, D_PAST_ATTACK_USED, 1)
    log("GuaHeath: used 没有时间了…… this turn")
end

function gsound_mindheart_damage()
    if potency(SELF, MIND_HEART) <= 0 then return end
    local id = current_skill_id()
    if id ~= SKILL_ONE and id ~= SKILL_TWO and id ~= 1079703 then return end
    dmgmult(clamp(math.floor(potency(SELF, BREATH) / 3) * 3, 0, 100))
end

function gsound_blade_field_damage()
    local bonus = 0
    local blood_stack = safe_number(safe_call(getgsoundstagefield, "blooddinner"))
    local scorch_stack = safe_number(safe_call(getgsoundstagefield, "firefield"))
    if blood_stack >= 2 and total(TARGET, "Laceration") > 0 then
        bonus = bonus + math.min(math.floor(blood_stack / 2), 100)
    end
    if scorch_stack >= 2 and total(TARGET, "Combustion") > 0 then
        bonus = bonus + math.min(math.floor(scorch_stack / 2), 100)
    end
    if bonus > 0 then
        dmgmult(bonus)
    end
end

function gsound_white_paper_start()
    setdata(SELF, D_BLANK_USED_WHITE, 1)
    local blood = safe_number(getgsoundstagefield("blooddinner"))
    if blood >= 75 then
        gsoundstagefield("blooddinner", "sub", 75, SELF)
        buff(SELF, "BloodArmorPersonalityFirst", 2, 0, 0)
        log("GuaHeath: spent 75 BloodDinner -> 2 Hardblood (had " .. blood .. ")")
    else
        buff(SELF, "Laceration", 5, 0, 0)
        log("GuaHeath: BloodDinner consume failed have=" .. blood .. " need=75")
    end
    -- The unlock callback normally starts these two persistent effects.  Call
    -- both again at the actual skill entry as a safety net for dashboard
    -- refreshes and for a White Paper that was inserted during a round.
    set_white_paper_fog(true)
    -- White Paper always starts with a full magazine, even if the earlier
    -- unlock/round-start reload was skipped by dashboard timing.
    local bullets = safe_number(getbuff(SELF, "AccelBullet", "stack"))
    if bullets < 10 then
        buff(SELF, "AccelBullet", 10 - bullets, 0, 0)
    end

    local explicator = potency(SELF, EXPLICATOR)
    local fire_poker = potency(SELF, FIRE_POKER)
    -- Snapshot both fields before consuming the blades. Their conditional
    -- damage and field visual remain active through all nine Coins.
    setdata(SELF, D_WHITE_PAPER_EXPLICATOR, explicator)
    setdata(SELF, D_WHITE_PAPER_FIRE_POKER, fire_poker)
    setdata(SELF, D_WHITE_PAPER_ACTIVE, 1)
    set_white_paper_fog(true)
    local blade_total = explicator + fire_poker
    local damage_bonus = clamp(blade_total * 5, 0, 50)
    if explicator == fire_poker and blade_total > 0 then
        damage_bonus = damage_bonus + 30
    end
    dmgmult(damage_bonus)
    if explicator > 0 then buff(SELF, EXPLICATOR, -explicator, 0, 0) end
    if fire_poker > 0 then buff(SELF, FIRE_POKER, -fire_poker, 0, 0) end
    setdata(SELF, D_WHITE_PAPER_READY, 0)
    setdata(SELF, D_WHITE_PAPER_GRANTED, 0)
    setdata(SELF, D_WHITE_PAPER_LAST_ROUND, safe_number(safe_call(getround)))
    setdata(SELF, D_DUAL_BLADE_NEXT, 0)
    log("GuaHeath: White Paper fired with " .. blade_total .. " blade stacks")
end

function gsound_other_skill_end()
    check_relocation_unlock(SELF)
    -- Restore the standby fog only if White Paper is actually on the dashboard.
    -- (GRANTED is set by round_start when the skill is inserted; during the unlock
    -- round it is still 0, so the fog no longer appears one round too early.)
    if safe_number(getdata(SELF, D_WHITE_PAPER_READY)) == 1
        and safe_number(getdata(SELF, D_WHITE_PAPER_GRANTED)) == 1 then
        set_white_paper_fog(true)
    end
end

function gsound_white_paper_end()
    set_white_paper_fog(false)
    setdata(SELF, D_WHITE_PAPER_ACTIVE, 0)
    setdata(SELF, D_WHITE_PAPER_EXPLICATOR, 0)
    setdata(SELF, D_WHITE_PAPER_FIRE_POKER, 0)
    -- EndSkill is the reliable fallback for a nine-coin attack: it also runs
    -- when the target dies before every coin can resolve.
    local bullets = safe_number(getbuff(SELF, "AccelBullet", "stack"))
    if bullets < 10 then
        buff(SELF, "AccelBullet", 10 - bullets, 0, 0)
    end
    check_relocation_unlock(SELF)
    log("GuaHeath: White Paper finished; magazine restored to 10")
end

function gsound_white_paper_burst3()
    burst(TARGET, 3)
    buff(TARGET, "Vibration", 0, -3, 0)
end

function gsound_white_paper_finale()
    buff(TARGET, "Sinking", 5, 5, 0)
    deluge(TARGET, 1)
    burst(TARGET, 10)
    buff(TARGET, "Vibration", 0, -10, 0)
end

-- ===== Coin motion-frame swaps (ChangeMotion timing) =====
-- These only change which animation frame each coin plays. Coin effects
-- (abilityScriptList) are untouched.
function gsound_past_coin1_motion()
    -- 过去 coin 1: play 白纸 coin 4 frame (S7 index 4)
    try_call(changemotion, "S7", 4)
end

function gsound_past_coin2_motion()
    -- 过去 coin 2: play 白纸 coin 5 frame (S7 index 5)
    try_call(changemotion, "S7", 5)
end

function gsound_past_coin3_motion()
    -- 过去 coin 3: play 白纸 coin 6 frame (S7 index 6)
    try_call(changemotion, "S7", 6)
end

function gsound_pastattack_coin1_motion()
    -- 没有时间了 coin 1: play 次女的阴阳师 coin 1 motion (S1 index 1)
    try_call(changemotion, "S1", 1)
end

function gsound_pastattack_coin2_motion()
    -- 没有时间了 coin 2: play 次女的阴阳师 coin 2 motion (S1 index 2)
    try_call(changemotion, "S1", 2)
end

function gsound_pastattack_coin3_motion()
    -- 没有时间了 coin 3: play 还有大把时间可以浪费 coin 2 motion (S5 index 2)
    try_call(changemotion, "S5", 2)
end

-- ===== Rooftop coin motion swap =====
-- Swap coin1/2 motions with coin3/4 motions (original frames S3#1..#5).
function gsound_rooftop_coin1_motion()
    -- coin 1 plays White Paper coin 1 frame (S7 index 1)
    try_call(changemotion, "S7", 1)
end

function gsound_rooftop_coin2_motion()
    -- coin 2 plays White Paper coin 2 frame (S7 index 2)
    try_call(changemotion, "S7", 2)
end

function gsound_rooftop_coin3_motion()
    -- coin 3 plays White Paper coin 3 frame (S7 index 3)
    try_call(changemotion, "S7", 3)
end

function gsound_rooftop_coin4_motion()
    -- coin 4 plays White Paper coin 4 frame (S7 index 4)
    try_call(changemotion, "S7", 4)
end

function gsound_rooftop_coin5_motion()
    -- coin 5 plays White Paper coin 5 frame (S7 index 5)
    try_call(changemotion, "S7", 5)
end

function gsound_rooftop_power()
    final(clamp(math.floor(potency(TARGET, "Sinking") / 5), 0, 3))
    scale(clamp(math.floor(potency(TARGET, "Vibration") / 5), 0, 3))
end

function gsound_rooftop_start()
    setdata(SELF, D_BLANK_USED_ROOFTOP, 1)
    local blood = safe_number(getgsoundstagefield("blooddinner"))
    if blood >= 50 then
        gsoundstagefield("blooddinner", "sub", 50, SELF)
        buff(SELF, "BloodArmorPersonalityFirst", 1, 0, 0)
        log("GuaHeath: spent 50 BloodDinner -> 1 Hardblood (had " .. blood .. ")")
    else
        buff(SELF, "Laceration", 5, 0, 0)
        log("GuaHeath: BloodDinner consume failed have=" .. blood .. " need=50")
    end
    -- Read after this skill's BloodDinner payment so the current 50 points
    -- count toward the shared cumulative threshold as well.
    local common_blood_used = safe_number(getgsoundstagefield("blooddinnerused"))
    local common_blood_bonus = clamp(math.floor(common_blood_used / 100) * 20, 0, 100)
    if common_blood_bonus > 0 then
        dmgmult(common_blood_bonus)
    end
    set_white_paper_fog(false)
    set_rooftop_aura(true)
    gsound_rooftop_power()
    local hope = safe_number(getbuff(SELF, "Hope", "stack"))
    if hope >= 3 then
        buff(SELF, "Hope", -3, 0, 0)
    end
    local bullets = safe_number(getbuff(SELF, "AccelBullet", "stack"))
    local spend = math.min(5, bullets)
    if spend > 0 then
        buff(SELF, "AccelBullet", -spend, 0, 0)
        dmgmult(spend * 10)
    end
    log("GuaHeath: 天台 used, spent " .. spend .. " SpeechDraft; common BloodDinner used=" .. common_blood_used .. ", bonus=" .. common_blood_bonus .. "%")
end

function gsound_rooftop_end()
    set_rooftop_aura(false)
    if safe_number(getdata(SELF, D_WHITE_PAPER_READY)) == 1
        and safe_number(getdata(SELF, D_WHITE_PAPER_GRANTED)) == 1 then
        set_white_paper_fog(true)
    end
    log("GuaHeath: 天台 finished")
end

-- ========================================================================
-- 空白领域 / 咒灵操术剑
-- ========================================================================

local function clear_old_form_resources()
    local old_buffs = {
        BULLET, EXPLICATOR, FIRE_POKER, LOVER, HATER, REUNION, FAREWELL, HOPE
    }
    for _, keyword in ipairs(old_buffs) do
        local amount = potency(SELF, keyword)
        if amount > 0 then buff(SELF, keyword, -amount, 0, 0) end
    end
end

local function blank_burst(unit, times)
    for _ = 1, times do
        burst(unit, 1)
        if count(unit, "Vibration") > 0 then
            buff(unit, "Vibration", 0, -1, 0)
        end
    end
end

local function blank_fixed_damage(unit, amount)
    amount = clamp(math.floor(safe_number(amount)), 0, 999)
    if amount <= 0 then return false end
    -- bonusdmg with both resistance selectors set to -1 uses Modular's
    -- GiveAbsHpDamage branch, i.e. genuine fixed HP damage.
    if type(bonusdmg) == "function" then
        safe_call(bonusdmg, unit, amount, -1, -1)
        return true
    end
    return false
end

local function blank_target_max_hp(unit)
    local value = safe_number(safe_call(gethp, unit, "max"))
    if value <= 0 then value = safe_number(safe_call(gethp, unit, "Max")) end
    if value <= 0 then value = 260 end
    return value
end

function gsound_blank_init()
    BLANK_DOMAIN_LATCHED = false
    -- Reset bridge-side pointer state explicitly for retries/new encounters.
    try_call(gsoundmotion, "reset", SELF)
    setdata(SELF, D_BLANK_QUEUED, 0)
    setdata(SELF, D_BLANK_USED_WHITE, 0)
    setdata(SELF, D_BLANK_USED_ROOFTOP, 0)
    setdata(SELF, D_BLANK_ENTERED, 0)
    setdata(SELF, D_BLANK_S2_REUSE, 0)
    setdata(SELF, D_BLANK_S3_SPENT, 0)
    setdata(SELF, D_BLANK_S3_REUSE, 0)
    setdata(SELF, D_BLANK_COUNTER_REUSE, 0)
    setdata(SELF, D_BLANK_SKILL_HEAL, 0)
    setdata(SELF, D_BLANK_COUNTERS_THIS_ROUND, 0)
    setdata(SELF, D_BLANK_INCOMING_DAMAGE, 0)
    setdata(SELF, D_BLANK_COUNTER_SKILL, 0)
    setdata(SELF, D_BLANK_GUARD_EQUIPPED, 0)
    setdata(SELF, D_BLANK_COUNTER_VARIANT, 0)
end

function gsound_blank_round_end()
    if is_blank_domain(SELF) then return end
    if safe_number(getdata(SELF, D_BLANK_QUEUED)) == 1 then return end
    if safe_number(getdata(SELF, D_BLANK_USED_WHITE)) ~= 1 then return end
    if safe_number(getdata(SELF, D_BLANK_USED_ROOFTOP)) ~= 1 then return end

    local blood_used = safe_number(safe_call(getgsoundstagefield, "blooddinnerused"))
    local fire_field = safe_number(safe_call(getgsoundstagefield, "firefield"))
    if blood_used >= 999 and fire_field >= 999 then
        setdata(SELF, D_BLANK_QUEUED, 1)
        log("Gsound: Blank Domain queued for next round")
    end
end

function gsound_blank_round_start()
    setdata(SELF, D_BLANK_COUNTERS_THIS_ROUND, 0)
    setdata(SELF, D_BLANK_COUNTER_REUSE, 0)
    setdata(SELF, D_BLANK_INCOMING_DAMAGE, 0)
    setdata(SELF, D_BLANK_COUNTER_SKILL, 0)
    setdata(SELF, D_BLANK_GUARD_EQUIPPED, 0)
    setdata(SELF, D_BLANK_COUNTER_VARIANT, 0)

    if safe_number(getdata(SELF, D_BLANK_QUEUED)) == 1
        and not is_blank_domain(SELF) then
        BLANK_DOMAIN_LATCHED = true
        setdata(SELF, D_BLANK_ENTERED, 1)
        setdata(SELF, D_BLANK_QUEUED, 0)
        buff(SELF, BLANK_DOMAIN, 1, 0, 0)
        -- Start the native presentation immediately after committing the
        -- permanent form flag. Cleanup calls below must never be able to skip
        -- the model swap, effect rebinding, or transition animation.
        try_call(gsoundmotion, "transform", SELF)
        clear_old_form_resources()
        setdata(SELF, D_WHITE_PAPER_READY, 0)
        setdata(SELF, D_WHITE_PAPER_GRANTED, 0)
        setdata(SELF, D_WHITE_PAPER_ACTIVE, 0)
        set_white_paper_fog(false)
        try_call(gsoundbgm, "start")
        safe_call(destroybuff, SELF, "Negative", 2, "All")
        safe_call(breakrecover, SELF)
        safe_call(healhp, SELF, 99999)
        local current_sp = safe_number(safe_call(getsp, SELF))
        safe_call(changesp, SELF, 45 - current_sp)
        log("Gsound: entered Blank Domain")
    end

    if is_blank_domain(SELF) then
        -- Stage fields remain registered and keep their current 999 stacks.
        try_call(gsoundstagefield, "blooddinner", "ensure", 0)
        try_call(gsoundstagefield, "firefield", "ensure", 0)
        try_call(gsoundbgm, "start")
    end
end

local function blank_skill_replacement(id)
    if id == SKILL_ONE or id == FUTURE_ATTACK or id == PAST_ATTACK then
        return BLANK_SKILL_ONE
    end
    if id == SKILL_TWO then return BLANK_SKILL_TWO end
    if id == SKILL_THREE or id == WHITE_PAPER or id == ROOFTOP then
        return BLANK_SKILL_THREE
    end
    if id == FUTURE or id == PAST or id == PRESENT then
        return BLANK_COUNTER
    end
    return 0
end

function gsound_blank_after_slots()
    if not is_blank_domain(SELF) then return end
    -- AfterSlots may lack getdata, but skillslotreplace is the reason this
    -- timing exists. Call the bridge first so replacement does not depend on
    -- which Modular Lua names this timing actually injected.
    if type(gsoundblankslots) == "function" then
        gsoundblankslots(SELF)
    end
    local replacements = {
        { SKILL_ONE, BLANK_SKILL_ONE },
        { FUTURE_ATTACK, BLANK_SKILL_ONE },
        { PAST_ATTACK, BLANK_SKILL_ONE },
        { SKILL_TWO, BLANK_SKILL_TWO },
        { SKILL_THREE, BLANK_SKILL_THREE },
        { WHITE_PAPER, BLANK_SKILL_THREE },
        { ROOFTOP, BLANK_SKILL_THREE },
        { FUTURE, BLANK_COUNTER },
        { PAST, BLANK_COUNTER },
        { PRESENT, BLANK_COUNTER }
    }
    if type(skillslotreplace) == "function" then
        for _, pair in ipairs(replacements) do
            skillslotreplace("All", pair[1], pair[2])
        end
    end
    if type(refreshallslotvisual) == "function" then
        refreshallslotvisual()
    end
    if type(log) == "function" then
        log("Gsound: Blank Domain dashboard slots replaced")
    end
end

function gsound_blank_before_use()
    if not is_blank_domain(SELF) then return end
    -- Execution-time fallback for a slot generated by a special combat path
    -- after AfterSlots (extra slots, defense switching, or a scripted skill).
    local current_id = current_skill_id()
    local replacement = blank_skill_replacement(current_id)

    if replacement ~= 0 then
        safe_call(changeskill, replacement)
        safe_call(refreshallslotvisual)
    end
end

-- Incoming one-sided damage is handled at the native damage calculation.
-- dmgmult in a defender passive modifies the wrong unit's outgoing damage.

function gsound_blank_clash_end()
    if not is_blank_domain(SELF) then return end
    buff(SELF, BREATH, 10, 10, 0)
end

function gsound_blank_before_attack()
    if not is_blank_domain(SELF) then return end
    -- Covers every current Blank Domain attack, including the three-Coin
    -- ordinary counter. Out-of-range indices are ignored by Modular.
    for index = 0, 11 do safe_call(makeunbreakable, index) end
end

function gsound_blank_hp_changed()
    if not is_blank_domain(SELF) then return end
    local hp_damage = safe_number(safe_call(gethpdmg))
    if hp_damage > 0 then setdata(SELF, D_BLANK_INCOMING_DAMAGE, hp_damage) end
    local stagger_state = safe_number(safe_call(getunitstate, SELF, "stagger"))
    if stagger_state == 1 then safe_call(breakrecover, SELF) end
end

function gsound_cursed_sword_hit()
    if not is_blank_domain(SELF) then return end
    local id = current_skill_id()
    if id ~= BLANK_SKILL_ONE and id ~= BLANK_SKILL_TWO and id ~= BLANK_SKILL_THREE
        and id ~= BLANK_COUNTER_TWO and id ~= BLANK_COUNTER_THREE then return end
    apply_all_abnormalities(TARGET, 10, 10)
    blank_burst(TARGET, 1)
    gain_cursed_spirit(1)
end

function gsound_cursed_sword_kill()
    if not is_blank_domain(SELF) then return end
    local enemy_count = math.max(1, safe_number(safe_call(getunitcount, "EveryEnemy")))
    local keywords = { "Laceration", "Combustion", "Sinking", "Vibration", "Burst" }
    for _, keyword in ipairs(keywords) do
        local shared_stack = math.floor(potency(TARGET, keyword) / enemy_count)
        local shared_turn = math.floor(count(TARGET, keyword) / enemy_count)
        if shared_stack > 0 or shared_turn > 0 then
            buff("EveryEnemy", keyword, shared_stack, shared_turn, 0)
        end
    end
end

function gsound_blank_s1_power()
    local negatives = safe_number(safe_call(getbuffcount, TARGET, "neg"))
    final(clamp(negatives, 0, 5))
end

function gsound_blank_s1_start()
    setdata(SELF, D_BLANK_SKILL_HEAL, 0)
    buff(SELF, "BlankDomainOneDamage", 1, 0, 0)
    local spirit = clamp(potency(SELF, CURSED_SPIRIT), 0, 100)
    dmgmult(spirit * 2 + clamp(math.floor(spirit / 10) * 15, 0, 150))
end

function gsound_blank_s1_end()
    local amount = potency(SELF, "BlankDomainOneDamage")
    if amount > 0 then buff(SELF, "BlankDomainOneDamage", -amount, 0, 0) end
end

function gsound_blank_s1_coin1()
    apply_all_abnormalities(TARGET, 15, 15)
end

function gsound_blank_s1_coin2()
    blank_burst(TARGET, 3)
    gain_cursed_spirit(15)
    local already = safe_number(getdata(SELF, D_BLANK_SKILL_HEAL))
    local cap = math.floor(blank_target_max_hp(SELF) * 0.5)
    local amount = math.min(math.max(0, cap - already), math.floor(safe_number(safe_call(gethpdmg)) * 0.5))
    if amount > 0 then
        safe_call(healhp, SELF, amount)
        setdata(SELF, D_BLANK_SKILL_HEAL, already + amount)
    end
end

function gsound_blank_s2_power()
    local negatives = safe_number(safe_call(getbuffcount, TARGET, "neg"))
    scale(clamp(negatives * 2, 0, 10))
end

function gsound_blank_s2_start()
    setdata(SELF, D_BLANK_S2_REUSE, 0)
    local blood_used = safe_number(safe_call(getgsoundstagefield, "blooddinnerused"))
    local fire_field = safe_number(safe_call(getgsoundstagefield, "firefield"))
    dmgmult(clamp(potency(SELF, CURSED_SPIRIT), 0, 100) * 2
        + clamp(math.floor(blood_used / 100) * 10, 0, 100)
        + clamp(math.floor(fire_field / 100) * 10, 0, 100))
    local shield_value = safe_number(safe_call(getshield, TARGET))
    if shield_value > 0 then safe_call(shield, TARGET, -shield_value) end
    safe_call(destroybuff, TARGET, "Positive", 2, "All")
end

function gsound_blank_s2_coin1()
    apply_all_abnormalities(TARGET, 30, 30)
end

function gsound_blank_s2_coin2()
    blank_burst(TARGET, 10)
    gain_cursed_spirit(50)
end

function gsound_blank_s2_coin3_power()
    dmgmult(clamp(safe_number(getdata(SELF, D_BLANK_S2_REUSE)) * 50, 0, 150))
end

function gsound_blank_s2_coin3()
    local stagger_state = safe_number(safe_call(getunitstate, TARGET, "stagger"))
    local reused = safe_number(getdata(SELF, D_BLANK_S2_REUSE))
    if stagger_state == 1 and reused < 3 then
        setdata(SELF, D_BLANK_S2_REUSE, reused + 1)
        reusecoin(-1)
    end
end

function gsound_blank_s3_start()
    local spirit = clamp(potency(SELF, CURSED_SPIRIT), 0, 100)
    setdata(SELF, D_BLANK_S3_SPENT, spirit)
    setdata(SELF, D_BLANK_S3_REUSE, 0)
    atkweight(clamp(math.floor(spirit / 20), 0, 5))
    if spirit > 0 then buff(SELF, CURSED_SPIRIT, -spirit, 0, 0) end
end

function gsound_blank_s3_power()
    local spirit = clamp(safe_number(getdata(SELF, D_BLANK_S3_SPENT)), 0, 100)
    if spirit <= 0 then spirit = clamp(potency(SELF, CURSED_SPIRIT), 0, 100) end
    final(math.floor(spirit / 10))
    clash(clamp(math.floor(count(TARGET, "Sinking") / 10), 0, 5))
    base(clamp(math.floor(count(TARGET, "Combustion") / 10), 0, 5))
    scale(clamp(math.floor(count(TARGET, "Laceration") / 10), 0, 5))
    local damage_bonus = spirit * 2
        + clamp(math.floor(spirit / 10) * 50, 0, 500)
        + clamp(math.floor(count(TARGET, "Burst") / 10) * 100, 0, 500)
    if has_special_vibration(TARGET) then damage_bonus = damage_bonus + 100 end
    dmgmult(damage_bonus)
end

function gsound_blank_s3_coin1()
    blank_burst(TARGET, 10)
    gain_cursed_spirit(50)
    -- getbuff sums a multi-unit selector in Modular. Remove the main target's
    -- own amount so only the other enemies' abnormalities are copied back.
    local keywords = { "Laceration", "Combustion", "Sinking", "Vibration", "Burst" }
    for _, keyword in ipairs(keywords) do
        local all_stack = safe_number(safe_call(getbuff, "EveryEnemy", keyword, "stack"))
        local all_turn = safe_number(safe_call(getbuff, "EveryEnemy", keyword, "turn"))
        local extra_stack = math.max(0, all_stack - potency(TARGET, keyword))
        local extra_turn = math.max(0, all_turn - count(TARGET, keyword))
        if extra_stack > 0 or extra_turn > 0 then
            buff(TARGET, keyword, extra_stack, extra_turn, 0)
        end
    end
end

function gsound_blank_s3_coin2()
    local spirit = clamp(safe_number(getdata(SELF, D_BLANK_S3_SPENT)), 0, 100)
    local amount = math.min(999, math.floor(blank_target_max_hp(TARGET) * 0.25))
    local repeats = 1 + clamp(math.floor(spirit / 20), 0, 5)
    for _ = 1, repeats do blank_fixed_damage(TARGET, amount) end
end

function gsound_blank_s3_coin3_power()
    local hp_ratio = safe_number(safe_call(gethp, TARGET, "%"))
    if hp_ratio > 0 and hp_ratio < 50 then dmgmult(300) end
end

function gsound_blank_s3_coin3()
    local hp_ratio = safe_number(safe_call(gethp, TARGET, "%"))
    if type(_G) == "table" then safe_call(_G["break"], "EveryEnemy", "both") end
    local reused = safe_number(getdata(SELF, D_BLANK_S3_REUSE))
    if hp_ratio <= 0 and reused < 3 then
        setdata(SELF, D_BLANK_S3_REUSE, reused + 1)
        reusecoin(-1)
    end
end

function gsound_blank_s3_end()
    setdata(SELF, D_BLANK_S3_SPENT, 0)
    setdata(SELF, D_BLANK_S3_REUSE, 0)
end

function gsound_blank_counter_power()
    local negatives = safe_number(safe_call(getbuffcount, TARGET, "neg"))
    final(clamp(negatives, 0, 5))
end

function gsound_blank_counter_start()
    local used = safe_number(getdata(SELF, D_BLANK_COUNTERS_THIS_ROUND)) + 1
    setdata(SELF, D_BLANK_COUNTERS_THIS_ROUND, used)
    setdata(SELF, D_BLANK_COUNTER_REUSE, 0)
    setdata(SELF, D_BLANK_SKILL_HEAL, 0)
    -- Ordinary COUNTER already resolves once per enemy action. The data guard
    -- prevents additional effects from exceeding the written round cap.
    if used <= 3 then
        local spirit_bonus = clamp(math.floor(potency(SELF, CURSED_SPIRIT) / 10) * 20, 0, 200)
        local enemy_coin_bonus = clamp(safe_number(safe_call(getcoincount, TARGET, "og")) * 25, 0, 150)
        dmgmult(spirit_bonus + enemy_coin_bonus)
    end
end

-- S2/S3 remain ordinary attack definitions on the dashboard. Their hidden
-- COUNTER clones carry the same wrappers after battle-start selection.
function gsound_blank_counter_variant_start()
    local id = current_skill_id()
    if id ~= BLANK_COUNTER_TWO and id ~= BLANK_COUNTER_THREE then return end
    gsound_blank_counter_start()
end

function gsound_blank_counter_variant_end()
    local id = current_skill_id()
    if id ~= BLANK_COUNTER_TWO and id ~= BLANK_COUNTER_THREE then return end
    gsound_blank_counter_end()
end

function gsound_blank_counter_coin1()
    if safe_number(getdata(SELF, D_BLANK_COUNTERS_THIS_ROUND)) > 3 then return end
    apply_all_abnormalities(TARGET, 20, 20)
end

function gsound_blank_counter_coin2_power()
    if safe_number(getdata(SELF, D_BLANK_COUNTERS_THIS_ROUND)) > 3 then return end
    if has_special_vibration(TARGET) then dmgmult(100) end
end

function gsound_blank_counter_coin2()
    if safe_number(getdata(SELF, D_BLANK_COUNTERS_THIS_ROUND)) > 3 then return end
    blank_burst(TARGET, 5)
    gain_cursed_spirit(25)
end

function gsound_blank_counter_coin3()
    if safe_number(getdata(SELF, D_BLANK_COUNTERS_THIS_ROUND)) > 3 then return end
    local incoming = clamp(safe_number(getdata(SELF, D_BLANK_INCOMING_DAMAGE)), 0, 333)
    blank_fixed_damage(TARGET, incoming * 3)

    local already = safe_number(getdata(SELF, D_BLANK_SKILL_HEAL))
    local cap = math.floor(blank_target_max_hp(SELF) * 0.3)
    local amount = math.min(math.max(0, cap - already), math.floor(safe_number(safe_call(gethpdmg)) * 0.3))
    if amount > 0 then
        safe_call(healhp, SELF, amount)
        setdata(SELF, D_BLANK_SKILL_HEAL, already + amount)
    end

    local stagger_state = safe_number(safe_call(getunitstate, TARGET, "stagger"))
    if stagger_state == 1 and safe_number(getdata(SELF, D_BLANK_COUNTER_REUSE)) == 0 then
        setdata(SELF, D_BLANK_COUNTER_REUSE, 1)
        reusecoin(-1)
    end
end

function gsound_blank_counter_end()
    setdata(SELF, D_BLANK_COUNTER_REUSE, 0)
end
