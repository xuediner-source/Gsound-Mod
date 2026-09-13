using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using FMOD.Studio;
using FMODUnity;
using HarmonyLib;
using ModularSkillScripts;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Playables;

namespace GsoundStageFieldBridge
{
 [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
 [BepInDependency("GlitchGames.ModularSkillScripts", BepInDependency.DependencyFlags.HardDependency)]
 public sealed class Plugin : BasePlugin
 {
    public const string PluginGuid = "gsound.stagefield.bridge";
    public const string PluginName = "Gsound Stage Field Bridge";
    public const string PluginVersion = "1.11.25";

    internal static BepInEx.Logging.ManualLogSource Logger;
    private static UnityEngine.AssetBundle _actionSourceBundle;
    private static readonly Dictionary<string, UnityEngine.AssetBundle> _blankDomainActionBundles =
        new Dictionary<string, UnityEngine.AssetBundle>(StringComparer.OrdinalIgnoreCase);
    private Harmony _harmony;

    public override void Load()
    {
        Logger = Log;
        AddComponent<BlankDomainPresentationDriver>();
        // The private bundle supplies the copied Past/Rooftop action timelines.
        // Runtime donor binding remains disabled, so its extra character FX
        // and model are not instantiated.
        LoadPrivateActionSource();
        BlankDomainAssetLifetimeProbe.Initialize();
        MainClass.consequenceDict["gsoundstagefield"] = new StageFieldConsequence();
        MainClass.acquirerDict["getgsoundstagefield"] = new StageFieldAcquirer();
        MainClass.acquirerDict["getgsoundblankstate"] = new BlankDomainStateAcquirer();
        MainClass.consequenceDict["gsoundblankslots"] = new BlankDomainSlotConsequence();
        MainClass.consequenceDict["gsoundbgm"] = new GsoundBgmConsequence();
        MainClass.consequenceDict["gsoundpreyaura"] = new PreyAuraConsequence();
            MainClass.consequenceDict["gsoundblackfog"] = new BlackFogConsequence();
            MainClass.consequenceDict["gsoundrefreshfx"] = new RefreshAbilityEffectConsequence();
            MainClass.consequenceDict["gsoundrelocationfx"] = new RelocationFieldConsequence();
            MainClass.consequenceDict["gsoundmotion"] = new GsoundMotionConsequence();
        try
        {
            _harmony = new Harmony(PluginGuid);
            try
            {
            Patch(typeof(PassiveModel), "ShowOnLeftPassiveUI",
                typeof(GsoundHeatCoatPassiveModelDisplayPatch), "Prefix", null);
            // PassiveAbility's trivial getter is folded with unrelated native
            // methods. Filter real PassiveModel/UI data through the hooks below.
            Patch(typeof(UnitInformationPassiveInformation), "SetPassiveData",
                typeof(GsoundHeatCoatPassiveBoxDisplayPatch), "Prefix", null);
            Patch(typeof(UnitInformationPassiveTabContent), "SetPassiveBoxData",
                typeof(GsoundHeatCoatPassiveBoxDisplayPatch), "Prefix", null);
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(UnitInformationSkillListData)))
                if (method.Name == "SetPersonalityData" || method.Name == "SetUnitModelData" ||
                    method.Name == "SetDataForPersonalityInfo")
                    _harmony.Patch(method, null, new HarmonyMethod(AccessTools.Method(
                        typeof(GsoundHeatCoatPassiveDataDisplayPatch), "Postfix")));
            }
            catch (Exception ex)
            {
                // A UI hook failure must never disable battle presentation fixes.
                Log.LogWarning("Gsound heat-coat display hooks failed: " + ex.Message);
            }
            GsoundCounterAccess.Install(_harmony);
            Patch(typeof(UnitInformationSkillListData), "IsUnlockedPersonalityPassive",
                typeof(GsoundPassiveUnlockPatch), "Prefix", null);
            Patch(typeof(BattleUI.WaveUI), "CheckActiveStageBuffUI",
                typeof(WaveUI_CheckActiveStageBuffUI_Patch), "Prefix", "Postfix");
            Patch(typeof(StageBuffUI), "SetData",
                typeof(StageBuffUI_SetData_Patch), "Prefix", null);
            Patch(typeof(StageBuffManager), "Clear",
                typeof(StageBuffManager_Clear_Patch), null, "Postfix");
            Patch(typeof(StageBuffManager), "OnTakeHpDamage",
                typeof(StageBuffManager_OnTakeHpDamage_Patch), "Prefix", "Postfix");
            Patch(typeof(BattleUI.BattleUIRoot), "UpdateStageBuffUIOnStartRound",
                typeof(BattleUIRoot_UpdateStageBuffUIOnStartRound_Patch), null, "Postfix");
            Patch(typeof(StageBuffManager), "CheckBloodDinnerOnInitStage",
                typeof(StageBuffManager_CheckBloodDinnerOnInitStage_Patch), null, "Postfix");
            Patch(typeof(StageBuffManager), "CheckUnitModelUseBuff",
                typeof(StageBuffManager_CheckUnitModelUseBuff_Patch), null, "Postfix");
            try
            {
                Patch(typeof(StageBuffManager), "CheckUnitScriptInherits",
                    typeof(StageBuffManager_CheckUnitScriptInherits_Patch), null, "Postfix");
                Patch(typeof(BloodDinnerBuff), "AddStack",
                    typeof(BloodDinnerBuff_AddStack_MapFx_Patch), null, "Postfix");
                Patch(typeof(StageBuffManager), "OnWaveStart",
                    typeof(StageBuffManager_OnWaveStart_BloodFx_Patch), null, "Postfix");
            }
            catch (Exception ex)
            {
                Log.LogWarning("BloodDinner map-FX extra patches failed: " + ex.Message);
            }
            Patch(typeof(BattleUnitModel), "RecoverAllBreak",
                typeof(BattleUnitModel_RecoverAllBreak_GsoundMotion_Patch), "Prefix", "Postfix");
            Patch(typeof(BattleUnitView), "SetUnRetreat",
                typeof(BattleUnitView_SetUnRetreat_GsoundMotion_Patch), null, "Postfix");
            Patch(typeof(BattleUnitView), "InitSkinInRuntime",
                typeof(BattleUnitView_InitSkinInRuntime_ThumbFx_Patch), null, "Postfix");
            Patch(typeof(BattleUnitView), "RefreshAppearanceRenderer",
                typeof(BattleUnitView_RefreshAppearanceRenderer_ThumbFx_Patch), null, "Postfix");
            Patch(typeof(SD.CharacterAppearance), "ChangeMotion",
                typeof(CharacterAppearance_ChangeMotion_BlankDomainVisual_Patch), "Prefix", "Postfix");
            Patch(typeof(SD.CharacterAppearance), "ChangeMotion_Parrying",
                typeof(CharacterAppearance_Parrying_BlankDomainVisual_Patch), "Prefix", "Postfix");
            Patch(typeof(SD.CharacterAppearance), "BindTimelineTracks",
                typeof(CharacterAppearance_BindTimeline_BlankDomain_Patch), null, "Postfix");
            Patch(typeof(SD.CharacterAppearance), "Update",
                typeof(CharacterAppearance_Update_BlankDomainVisual_Patch), null, "Postfix");
            Patch(typeof(SD.CharacterAppearance), "ChangeDefaultSpineRenderer",
                typeof(CharacterAppearance_DefaultSpine_BlankDomain_Patch), "Prefix", "Postfix");
            Patch(typeof(EffectActivateTimelineClip), "CreatePlayable",
                typeof(EffectActivateTimelineClip_BlankDomain_Patch), "Prefix", null);
            try { GsoundIllustrationAccess.Install(_harmony); }
            catch (Exception ex) { Log.LogWarning("Gsound dual illustration hooks: " + ex.Message); }
            GsoundVoiceMuteAccess.Patch(_harmony);
            Log.LogInfo("Harmony patches applied for Gsound StageBuffUI counters.");
        }
        catch (Exception ex)
        {
            Log.LogWarning("Harmony patch failed, falling back to manual refresh: " + ex);
        }
    Log.LogInfo("Registered Gsound's official field UI bridge v" + PluginVersion);
    }

    private void Patch(Type target, string methodName, Type patchType, string prefix, string postfix)
    {
        var original = AccessTools.Method(target, methodName);
        if (original == null)
            throw new MissingMethodException(target.FullName + "." + methodName);
        HarmonyMethod prefixMethod = prefix == null ? null : new HarmonyMethod(AccessTools.Method(patchType, prefix));
        HarmonyMethod postfixMethod = postfix == null ? null : new HarmonyMethod(AccessTools.Method(patchType, postfix));
        _harmony.Patch(original, prefixMethod, postfixMethod);
    }

    private void LoadPrivateActionSource()
    {
        try
        {
            string pluginRoot = ResolvePluginRoot();
            string bundlePath = Path.Combine(pluginRoot, "assets", "GsoundThumbActions.bundle");
            if (!File.Exists(bundlePath))
            {
                Log.LogWarning("Gsound private action source is missing: " + bundlePath);
            }
            else
            {
                _actionSourceBundle = UnityEngine.AssetBundle.LoadFromFile(bundlePath);
                if (_actionSourceBundle == null)
                    Log.LogError("Gsound private action source failed to load: " + bundlePath);
                else
                    Log.LogInfo("Loaded Gsound private action source before custom appearance initialization: " + bundlePath);
            }

            string assetRoot = Path.Combine(pluginRoot, "assets");
            if (!Directory.Exists(assetRoot))
            {
                Log.LogError("Gsound asset root is missing: " + assetRoot);
            }
            else
            {
                foreach (string blankPath in Directory.GetFiles(assetRoot, "GsoundBlank*.bundle"))
                {
                    UnityEngine.AssetBundle blankBundle = UnityEngine.AssetBundle.LoadFromFile(blankPath);
                    if (blankBundle == null)
                    {
                        Log.LogError("Gsound Blank Domain action source failed to load: " + blankPath);
                        continue;
                    }
                    _blankDomainActionBundles[Path.GetFileName(blankPath)] = blankBundle;
                    Log.LogInfo("Loaded Blank Domain action source: " + blankPath);
                }
            }
            BlankDomainVisualAccess.LoadNativeArt();
        }
        catch (Exception ex)
        {
            Log.LogError("Gsound private action source load failed: " + ex);
        }
    }

    internal static string ResolvePluginRoot()
    {
        // Lethe discovers bridge DLLs recursively, so a development assembly
        // can be loaded from a nested work folder.  Resolve assets from the
        // actual mod root instead of trusting Assembly.Location blindly.
        string current = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
        for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(current); depth++)
        {
            if (File.Exists(Path.Combine(current, "assets", "GsoundThumbActions.bundle")) &&
                Directory.Exists(Path.Combine(current, "custom_appearance")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        string known = Path.Combine(Paths.PluginPath, "Lethe", "mods", "Gsound");
        if (Directory.Exists(known))
            return known;
        return Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
    }

    internal static UnityEngine.GameObject InstantiatePrivateThumbDonor(UnityEngine.Transform parent)
    {
        if (_actionSourceBundle == null)
            return null;

        const string assetPath = "Assets/Resources_moved/Prefab/SD/10716_Heathcliff_Thumb_DiscipleAppearance.prefab";
        UnityEngine.GameObject prefab = _actionSourceBundle.LoadAsset<UnityEngine.GameObject>(assetPath);
        if (prefab == null)
            return null;
        return UnityEngine.Object.Instantiate(prefab, parent, false);
    }

    internal static UnityEngine.GameObject InstantiateBlankDomainDonor(
        string bundleName, string assetPath, UnityEngine.Transform parent)
    {
        UnityEngine.AssetBundle bundle;
        if (!_blankDomainActionBundles.TryGetValue(bundleName, out bundle) || bundle == null)
            return null;
        UnityEngine.GameObject prefab = bundle.LoadAsset<UnityEngine.GameObject>(assetPath);
        if (prefab == null)
            return null;
        return UnityEngine.Object.Instantiate(prefab, parent, false);
    }

    internal static UnityEngine.Sprite FindBlankDomainSprite(string bundleName, string spriteName)
    {
        UnityEngine.AssetBundle bundle;
        if (!_blankDomainActionBundles.TryGetValue(bundleName, out bundle) || bundle == null)
            return null;
        try
        {
            // LoadAllAssets<T>() marshals an IL2CPP array through ReadOnlySpan<T>
            // on this game build and throws MissingMethodException.  The idle
            // Sprite is explicitly named by the private bundle builder, so a
            // single-asset lookup avoids that incompatible array path.
            return bundle.LoadAsset<UnityEngine.Sprite>(spriteName);
        }
        catch (Exception ex)
        {
            Logger?.LogWarning("Blank Domain named Sprite lookup failed: " + ex.Message);
            return null;
        }
    }

    internal static UnityEngine.Sprite LoadBlankDomainIdlePng()
    {
        return LoadBlankDomainFrame("idle", 0.6505175983f, 0.0336729743f);
    }

    internal static UnityEngine.Sprite LoadBlankDomainFrame(string frame, float pivotX, float pivotY)
    {
        try
        {
            // ImageConversion.LoadImage and Il2CppStructArray both marshal through
            // ReadOnlySpan.GetPinnableReference on this IL2CPP build. Upload a
            // predecoded RGBA sidecar through the IntPtr LoadRawTextureData path.
            string path = Path.Combine(ResolvePluginRoot(), "assets", "blank_domain_" + frame + "_runtime.rgba");
            if (!File.Exists(path))
            {
                Logger?.LogError("Blank Domain runtime idle RGBA is missing: " + path);
                return null;
            }
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 8)
            {
                Logger?.LogError("Blank Domain runtime idle RGBA is truncated: " + path);
                return null;
            }
            int width = BitConverter.ToInt32(bytes, 0);
            int height = BitConverter.ToInt32(bytes, 4);
            int pixelBytes = width * height * 4;
            if (width <= 0 || height <= 0 || bytes.Length < 8 + pixelBytes)
            {
                Logger?.LogError("Blank Domain runtime idle RGBA header is invalid: " + path);
                return null;
            }
            UnityEngine.Texture2D texture = new UnityEngine.Texture2D(
                width, height, UnityEngine.TextureFormat.RGBA32, false);
            // CoreCLR fields are not Unity's native asset roots. Both objects
            // must survive the menu/battle UnloadUnusedAssets sweeps before a
            // SpriteRenderer has ever referenced them.
            texture.hideFlags |= UnityEngine.HideFlags.DontUnloadUnusedAsset;
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                IntPtr pixels = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + 8);
                texture.LoadRawTextureData(pixels, pixelBytes);
                texture.Apply(false, false);
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
            texture.name = "GsoundBlankDomain_" + frame + "_RuntimeTexture";
            texture.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            texture.filterMode = UnityEngine.FilterMode.Bilinear;
            UnityEngine.Sprite sprite = UnityEngine.Sprite.Create(
                texture,
                new UnityEngine.Rect(0f, 0f, width, height),
                new UnityEngine.Vector2(pivotX, pivotY),
                100f);
            sprite.hideFlags |= UnityEngine.HideFlags.DontUnloadUnusedAsset;
            sprite.name = "GsoundBlankDomain_" + frame + "_Runtime";
            Logger?.LogInfo("Blank Domain frame asset created: " + frame + "; sprite=" + sprite.GetInstanceID() +
                "; texture=" + texture.GetInstanceID() + "; protected=" + sprite.hideFlags + "/" + texture.hideFlags);
            return sprite;
        }
        catch (Exception ex)
        {
            Logger?.LogError("Blank Domain runtime idle RGBA load failed: " + ex);
            return null;
        }
    }
 }

 internal static class GsoundIllustrationAccess
 {
    private static UnityEngine.Sprite normal, awakened;
    internal static UnityEngine.Sprite Select(int personalityId, bool gacksung)
    {
        if (personalityId != 107970) return null;
        var sprite = gacksung ? awakened : normal;
        if (sprite != null && sprite.texture != null) return sprite;
        sprite = Plugin.LoadBlankDomainFrame(gacksung ? "cg_awakened" : "cg_normal", .5f, .5f);
        if (gacksung) awakened = sprite; else normal = sprite;
        return sprite;
    }
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(PlayerUnitSpriteList), "GetCGData"),
            null, new HarmonyMethod(AccessTools.Method(typeof(GsoundIllustrationAccess), "CGPostfix")) { priority = Priority.Last });
        harmony.Patch(AccessTools.Method(typeof(PlayerUnitSpriteList), "GetCGDataWithCallback"),
            new HarmonyMethod(AccessTools.Method(typeof(GsoundIllustrationAccess), "CGCallbackPrefix")));
        // IL2CPP folds these field getters with hundreds of unrelated methods.
        // Patching them globally sent AbilityData / UnitSinModel into CG code.
        // Populate the genuine illustration data after its unique SetData bodies.
        foreach (var argument in new[] { typeof(UnitModel), typeof(IPersonality) })
            harmony.Patch(AccessTools.Method(typeof(UnitInformationIllustData), "SetData", new[] { argument }), null,
                new HarmonyMethod(AccessTools.Method(typeof(GsoundIllustrationAccess), "DataPostfix")));
        harmony.Patch(AccessTools.Method(typeof(UnitInformationIllustData), "GetNormalIllustAsync"),
            new HarmonyMethod(AccessTools.Method(typeof(GsoundIllustrationAccess), "NormalAsyncPrefix")));
        harmony.Patch(AccessTools.Method(typeof(UnitInformationIllustData), "GetGacksungIllustAsync"),
            new HarmonyMethod(AccessTools.Method(typeof(GsoundIllustrationAccess), "AwakenedAsyncPrefix")));
        Plugin.Logger?.LogInfo("Gsound two illustrations connected to native normal/awakened selection.");
    }
    private static void CGPostfix(int personalityId, bool gacksung, ref UnityEngine.Sprite __result)
    {
        var sprite = Select(personalityId, gacksung);
        if (sprite != null) __result = sprite;
    }
    private static bool CGCallbackPrefix(int personalityId, bool gacksung,
        ref Il2CppSystem.ValueTuple<UnityEngine.Sprite, DelegateEvent> __result)
    {
        var sprite = Select(personalityId, gacksung);
        if (sprite == null) return true;
        __result = new Il2CppSystem.ValueTuple<UnityEngine.Sprite, DelegateEvent>(sprite, null);
        return false;
    }
    internal static void DataPostfix(UnitInformationIllustData __instance)
    {
        if (__instance == null || __instance._personalityId != 107970) return;
        var first = Select(107970, false);
        var second = Select(107970, true);
        if (first != null) __instance._leftSideIllust = first;
        if (second != null)
        {
            __instance._leftSideIllust_Gacksung = second;
            __instance._isIllustChangeable = true;
        }
    }
    private static bool NormalAsyncPrefix(UnitInformationIllustData __instance, Il2CppSystem.Action<UnityEngine.Sprite> onLoadAsset)
    {
        var sprite = Select(__instance._personalityId, false);
        if (sprite == null) return true;
        __instance._leftSideIllust = sprite;
        onLoadAsset?.Invoke(sprite);
        return false;
    }
    private static bool AwakenedAsyncPrefix(UnitInformationIllustData __instance, Il2CppSystem.Action<UnityEngine.Sprite> onLoadAsset)
    {
        var sprite = Select(__instance._personalityId, true);
        if (sprite == null) return true;
        __instance._leftSideIllust_Gacksung = sprite;
        onLoadAsset?.Invoke(sprite);
        return false;
    }

 }

 internal static class GsoundRecoverMotionAccess
 {
    private static readonly HashSet<IntPtr> Pending = new HashSet<IntPtr>();
    private static readonly HashSet<IntPtr> Played = new HashSet<IntPtr>();

    internal static void Arm(BattleUnitModel model)
    {
        if (model == null || model.Pointer == IntPtr.Zero)
            return;
        if (!StageFieldAccess.IsGsoundUnit(model) ||
            (Played.Contains(model.Pointer) && !BlankDomainStateAccess.IsEntered(model)))
            return;

        try
        {
            // The passive only recovers an ordinary first stagger. Forced
            // stagger is intentionally excluded to match its written effect.
            if (!model.IsBreak() || model.IsForcelyBreak())
                return;
            Pending.Add(model.Pointer);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Gsound recover-motion arm failed: " + ex.Message);
        }
    }

    internal static void PlayIfArmed(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero)
            return;

        BattleUnitModel model = view.unitModel;
        if (model == null || model.Pointer == IntPtr.Zero)
            return;
        IntPtr key = model.Pointer;
        if (!Pending.Remove(key) ||
            (Played.Contains(key) && !BlankDomainStateAccess.IsEntered(model)))
            return;

        try
        {
            SD.CharacterAppearance appearance = view.Appearance;
            if (appearance == null)
                return;
            // RoundStart Blank Domain recover must not restart Special1 while
            // the transform cinematic is already using that motion.
            if (BlankDomainVisualAccess.IsTransforming(appearance))
                return;

            if (model.IsBreak()) return;
            if (BlankDomainStateAccess.IsEntered(model))
            {
                BlankDomainVisualAccess.BeginRecovery(view);
                return;
            }
            // Thumb Father Rodion's stagger-recovery presentation is stored
            // as Special1 rather than as a standalone UnRetreat motion. The
            // Gsound appearance retains that timeline with redrawn frames.
            // Mute the donor timeline audio so Rodion's voice cannot leak.
            GsoundVoiceMuteAccess.MuteAppearance(view);
            appearance.ChangeMotion(MOTION_DETAIL.Special1, true, 0, false, null, true);
            Played.Add(key);
            Plugin.Logger?.LogInfo("Played Gsound first-stagger recovery motion (Special1).");
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Gsound recover-motion playback failed: " + ex.Message);
        }
    }

    internal static void PlayTransformation(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero || view.Appearance == null)
            return;

        BlankDomainVisualAccess.BeginTransformation(view);
    }

    internal static void Clear()
    {
        Pending.Clear();
        Played.Clear();
    }
 }

 internal static class BlankDomainVisualAccess
 {
    private const string IdleObjectName = "Gsound_BlankDomain_RuntimeIdle";
    private static readonly HashSet<IntPtr> ActiveAppearances = new HashSet<IntPtr>();
    private static readonly HashSet<IntPtr> ActiveViewPointers = new HashSet<IntPtr>();
    private static readonly Dictionary<IntPtr, BattleUnitView> ActiveViews =
        new Dictionary<IntPtr, BattleUnitView>();
    private static readonly Dictionary<IntPtr, SD.CharacterAppearance> PresentationAppearances =
        new Dictionary<IntPtr, SD.CharacterAppearance>();
    private static readonly Dictionary<IntPtr, UnityEngine.SpriteRenderer> IdleRenderers =
        new Dictionary<IntPtr, UnityEngine.SpriteRenderer>();
    private static readonly Dictionary<IntPtr, bool> IdleModes =
        new Dictionary<IntPtr, bool>();
    private static readonly HashSet<IntPtr> ParryRemapLogged = new HashSet<IntPtr>();
    private static readonly HashSet<IntPtr> ActiveModels = new HashSet<IntPtr>();
    private static readonly HashSet<string> MotionDiagnostics = new HashSet<string>();
    private static readonly Dictionary<IntPtr, UnityEngine.MeshRenderer> HiddenSpineRenderers =
        new Dictionary<IntPtr, UnityEngine.MeshRenderer>();
    private static readonly Dictionary<IntPtr, bool> OriginalSpineRenderingOff = new Dictionary<IntPtr, bool>();
    private static readonly HashSet<IntPtr> TransformingAppearances = new HashSet<IntPtr>();
    private static readonly Dictionary<IntPtr, BattleUnitView> TransformingViews = new Dictionary<IntPtr, BattleUnitView>();
    private const float TransitionSourceDuration = 2.55f;
    internal const float TransitionPlaybackRate = 0.45f;
    private const float TransitionWhiteDuration = 1.10f;
    private sealed class TransitionClock
    {
        internal float Time;
        internal float PreludeTime;
        internal bool Started;
        internal bool Committed;
        internal PlayableDirector Director;
        internal DirectorUpdateMode PreviousMode;
        internal UnityEngine.Timeline.TimelineAsset Timeline;
    }
    private static readonly Dictionary<IntPtr, TransitionClock> TransitionClocks =
        new Dictionary<IntPtr, TransitionClock>();
    private static readonly Dictionary<IntPtr, Sequence> TransitionSequences =
        new Dictionary<IntPtr, Sequence>();
    private static readonly Dictionary<IntPtr, UnityEngine.GameObject> TransitionFlashes =
        new Dictionary<IntPtr, UnityEngine.GameObject>();
    private static readonly Dictionary<IntPtr, List<UnityEngine.GameObject>> SecondaryTrackObjects =
        new Dictionary<IntPtr, List<UnityEngine.GameObject>>();
    private static readonly Dictionary<IntPtr, bool> SecondaryHorseWildHunt =
        new Dictionary<IntPtr, bool>();
    // Index Father's BlackNightmare is a scene effect rather than a sprite
    // layer. Keep one cloned particle tree attached to the committed phase-2
    // appearance so native round/duel motion cannot replace it with phase-1.
    private const string PersistentShadowObjectName =
        "Gsound_BlankDomain_Persistent_BlackNightmare";
    private static readonly Dictionary<IntPtr, UnityEngine.GameObject> PersistentShadowEffects =
        new Dictionary<IntPtr, UnityEngine.GameObject>();
    private static float PersistentShadowRetryAt;
    private static bool PersistentShadowMissingLogged;
    private static float PersistentShadowHealthAt;
    private const string SecondaryTrailName = "Effect_Trail";
    private const string SecondaryTrail2Name = "Effect_Trail_2";
    private const string SecondaryHorseName = "Effect_Horse";
    private const string SecondaryCoffinName = "Effect_Coffin";
    private const string SecondarySlashName = "Effect_Slash";
    private static bool ClearingTransitions;
    private static UnityEngine.Sprite _idleSprite;
    private sealed class AuthoredFrame
    {
        internal double Time;
        internal string Frame;
    }
    private static readonly Dictionary<string, UnityEngine.Sprite> RuntimeFrames =
        new Dictionary<string, UnityEngine.Sprite>(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<AuthoredFrame>> AuthoredFrames =
        new Dictionary<string, List<AuthoredFrame>>(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<AuthoredFrame>> TimelineFrames =
        new Dictionary<string, List<AuthoredFrame>>(StringComparer.Ordinal);
    private static Sequence PresentationTicker;
    private static readonly Dictionary<IntPtr, bool> SavedDisableSpine = new Dictionary<IntPtr, bool>();
    private static readonly HashSet<string> PresentationChecks = new HashSet<string>();
    private static readonly Dictionary<string, UnityEngine.Vector2> FramePivots =
        new Dictionary<string, UnityEngine.Vector2> {
            { "idle", new UnityEngine.Vector2(0.6505175983f, 0.0336729743f) },
            { "windup", new UnityEngine.Vector2(0.4774220033f, 0.0134556310f) },
            { "slash", new UnityEngine.Vector2(0.6601903512f, 0.0308816811f) },
            { "followthrough", new UnityEngine.Vector2(0.6543561685f, 0.0270141427f) },
            { "thrust", new UnityEngine.Vector2(0.2997352239f, 0.0333975517f) },
            { "recover_low", new UnityEngine.Vector2(0.542604936f, 0.0556305293f) },
            { "recover_rise", new UnityEngine.Vector2(0.6372510738f, 0.0430349378f) },
            { "transform_start", new UnityEngine.Vector2(0.4467640817165375f, 0.08871791511774063f) }
        };
    private static float RetryArtAt;
    private static readonly Dictionary<IntPtr, float> RecoveryStarts = new Dictionary<IntPtr, float>();
    internal static string RecoveryFrame(float elapsed)
    {
        return elapsed < 0.55f ? "recover_low" : elapsed < 1.15f ? "recover_rise" : elapsed < 1.65f ? "idle" : null;
    }
    internal static void BeginRecovery(BattleUnitView view)
    {
        if (!IsActive(view) || view.Appearance == null || IsTransforming(view.Appearance)) return;
        RecoveryStarts[view.Appearance.Pointer] = UnityEngine.Time.time;
        EnsurePresentationTicker();
        PresentRecovery(view.Appearance);
        Plugin.Logger?.LogInfo("Blank Domain phase-2 recovery: kneel, rise, idle (1.65s).");
    }
    private static bool PresentRecovery(SD.CharacterAppearance appearance)
    {
        if (!RecoveryStarts.TryGetValue(appearance.Pointer, out var start)) return false;
        string frame = RecoveryFrame(Math.Max(0f, UnityEngine.Time.time - start));
        if (frame == null) { RecoveryStarts.Remove(appearance.Pointer); return false; }
        SetIdleMode(appearance, false);
        return PresentFrame(appearance, frame);
    }
    private static bool PresentFrame(SD.CharacterAppearance appearance, string frame)
    {
        EnsureRuntimeArtAlive("extra presentation");
        if (!RuntimeFrames.TryGetValue(frame, out var sprite) || sprite == null) return false;
        var renderer = EnsureIdleRenderer(appearance);
        if (renderer == null) return false;
        renderer.sprite = sprite;
        renderer.color = UnityEngine.Color.white;
        renderer.enabled = true;
        renderer.forceRenderingOff = false;
        return true;
    }

    internal static bool EnsureRuntimeArtAlive(string reason)
    {
        bool failed = false;
        bool attempted = false;
        foreach (var pair in FramePivots)
        {
            RuntimeFrames.TryGetValue(pair.Key, out var frame);
            if (frame != null && frame.texture != null) continue;
            if (UnityEngine.Time.unscaledTime < RetryArtAt) { failed = true; continue; }
            attempted = true;
            Plugin.Logger?.LogWarning("Blank Domain rebuilding lost frame: " + pair.Key + "; reason=" + reason +
                "; managedNull=" + ReferenceEquals(frame, null) + "; unityNull=" + (frame == null));
            if (frame != null) UnityEngine.Object.Destroy(frame);
            RuntimeFrames[pair.Key] = Plugin.LoadBlankDomainFrame(pair.Key, pair.Value.x, pair.Value.y);
            if (RuntimeFrames[pair.Key] == null) failed = true;
        }
        RuntimeFrames.TryGetValue("idle", out _idleSprite);
        if (attempted && failed) RetryArtAt = UnityEngine.Time.unscaledTime + 2f;
        return _idleSprite != null && _idleSprite.texture != null;
    }

    internal static string RuntimeArtStatus()
    {
        var status = new List<string>();
        foreach (var pair in FramePivots)
        {
            RuntimeFrames.TryGetValue(pair.Key, out var frame);
            status.Add(pair.Key + "=" + (frame != null && frame.texture != null));
        }
        return string.Join(",", status);
    }

    internal static bool OwnsPresentation(SD.CharacterAppearance appearance)
    {
        return appearance != null && ((IsTransforming(appearance) &&
            TransitionClocks.TryGetValue(appearance.Pointer, out var clock) && clock.Started) ||
            IsActive(appearance._battleUnitView)) &&
            EnsureRuntimeArtAlive("presentation gate") && EnsureIdleRenderer(appearance) != null;
    }

    internal static void SuppressDefaultSpine(SD.CharacterAppearance appearance)
    {
        if (!OwnsPresentation(appearance)) return;
        if (!SavedDisableSpine.ContainsKey(appearance.Pointer))
            SavedDisableSpine[appearance.Pointer] = appearance._isDisableSpine;
        // Native ChangeDefaultSpineRenderer reads _isDisableSpine before
        // selecting ENABLE/DISABLE, including duel approach and round idle.
        appearance.SetDisableSpine(true);
        var skin = appearance.currentSpineSkin;
        if (skin != null && skin._spine_renderer != null)
        {
            var mesh = skin._spine_renderer;
            if (!HiddenSpineRenderers.ContainsKey(mesh.Pointer))
            {
                HiddenSpineRenderers[mesh.Pointer] = mesh;
                OriginalSpineRenderingOff[mesh.Pointer] = mesh.forceRenderingOff;
            }
            mesh.forceRenderingOff = true;
        }
    }

    private static void EnsurePresentationTicker()
    {
        if (PresentationTicker != null && PresentationTicker.IsActive())
            return;
        // The transition already proves DOTween callbacks run in this IL2CPP
        // build. Keep the ticker alive explicitly and execute after animation.
        PresentationTicker = DOTween.Sequence().SetUpdate(UpdateType.Late, true);
        PresentationTicker.AppendInterval(1f);
        PresentationTicker.SetLoops(-1);
        PresentationTicker.OnUpdate((TweenCallback)(() => LateUpdatePresentation()));
        Plugin.Logger?.LogInfo("Blank Domain late presentation ticker started.");
    }

    internal static void LoadNativeArt()
    {
        _idleSprite = Plugin.LoadBlankDomainIdlePng();
        RuntimeFrames["idle"] = _idleSprite;
        RuntimeFrames["windup"] = Plugin.LoadBlankDomainFrame("windup", 0.4774220033f, 0.0134556310f);
        RuntimeFrames["slash"] = Plugin.LoadBlankDomainFrame("slash", 0.6601903512f, 0.0308816811f);
        RuntimeFrames["followthrough"] = Plugin.LoadBlankDomainFrame("followthrough", 0.6543561685f, 0.0270141427f);
        RuntimeFrames["thrust"] = Plugin.LoadBlankDomainFrame("thrust", 0.2997352239f, 0.0333975517f);
        RuntimeFrames["recover_low"] = Plugin.LoadBlankDomainFrame("recover_low", 0.542604936f, 0.0556305293f);
        RuntimeFrames["recover_rise"] = Plugin.LoadBlankDomainFrame("recover_rise", 0.6372510738f, 0.0430349378f);
        RuntimeFrames["transform_start"] = Plugin.LoadBlankDomainFrame("transform_start", 0.4467640817165375f, 0.08871791511774063f);
        try
        {
            string path = Path.Combine(Plugin.ResolvePluginRoot(), "assets", "blank_domain_frame_schedule.tsv");
            foreach (string line in File.ReadAllLines(path))
            {
                string[] columns = line.Split('\t');
                if (columns.Length != 3) continue;
                double time = double.Parse(columns[1], System.Globalization.CultureInfo.InvariantCulture);
                if (!RuntimeFrames.ContainsKey(columns[2])) throw new InvalidDataException("Unknown frame " + columns[2]);
                List<AuthoredFrame> frames;
                if (!AuthoredFrames.TryGetValue(columns[0], out frames))
                    AuthoredFrames[columns[0]] = frames = new List<AuthoredFrame>();
                frames.Add(new AuthoredFrame { Time = time, Frame = columns[2] });
            }
            Plugin.Logger?.LogInfo("Blank Domain authored frame timing loaded: " + AuthoredFrames.Count + " animation curves.");
            string timelinePath = Path.Combine(Plugin.ResolvePluginRoot(), "assets", "blank_domain_timeline_frames.tsv");
            foreach (string line in File.ReadAllLines(timelinePath))
            {
                string[] columns = line.Split('\t');
                if (columns.Length != 3) continue;
                double time = double.Parse(columns[1], System.Globalization.CultureInfo.InvariantCulture);
                if (!RuntimeFrames.ContainsKey(columns[2])) throw new InvalidDataException("Unknown frame " + columns[2]);
                if (!TimelineFrames.TryGetValue(columns[0], out var frames))
                    TimelineFrames[columns[0]] = frames = new List<AuthoredFrame>();
                frames.Add(new AuthoredFrame { Time = time, Frame = columns[2] });
            }
            Plugin.Logger?.LogInfo("Blank Domain direct timeline schedules loaded: " + TimelineFrames.Count);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Blank Domain authored timing unavailable; using bundle sampler: " + ex);
        }
        if (_idleSprite != null) return;
        string[] idleNames =
        {
            "GsoundBlankDomainIdle",
            "transform/idle.png",
            "idle"
        };
        for (int index = 0; index < idleNames.Length && _idleSprite == null; index++)
            _idleSprite = Plugin.FindBlankDomainSprite("GsoundBlankTransform.bundle", idleNames[index]);
        if (_idleSprite != null)
        {
            Plugin.Logger?.LogInfo("Loaded Blank Domain native idle Sprite by name from transform bundle.");
            return;
        }

        _idleSprite = Plugin.LoadBlankDomainIdlePng();
        if (_idleSprite == null)
            Plugin.Logger?.LogError("Blank Domain idle Sprite is unavailable from both bundle and RGBA fallback.");
        else
            Plugin.Logger?.LogInfo("Loaded Blank Domain idle Sprite from RGBA fallback.");
    }

    internal static void BeginTransformation(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero || view.Appearance == null)
            return;

        SD.CharacterAppearance appearance = view.Appearance;
        UnityEngine.GameObject flashRoot = null;
        try
        {
            // Play private detail 21 (Wild Hunt Ride_Start, FileID 11 =
            // GsoundBlankTransform.bundle). That clip's pptr mapping is the
            // redrawn idle / pose1 / pose2 / mov sprites. Special1 stays the
            // first-stagger recovery slot and must not be reused here.
            ThumbActionEffectBindingAccess.Remove(view);
            BlankDomainActionEffectBindingAccess.Remove(view);
            TransformingAppearances.Add(appearance.Pointer);
            TransformingViews[appearance.Pointer] = view;
            TransitionClocks[appearance.Pointer] = new TransitionClock();
            EnsurePresentationTicker();
            appearance.SetFocusing(true);
            view.SetFocusByCam(true);
            try
            {
                BattleCamManager.Instance.SetFocusingTarget(view.transform);
                BattleCamManager.Instance.StartFocusing();
            }
            catch (Exception cameraEx)
            {
                Plugin.Logger?.LogWarning("Blank Domain camera focus fallback: " + cameraEx.Message);
            }

            GsoundVoiceMuteAccess.MuteAppearance(view);
            flashRoot = new UnityEngine.GameObject("Gsound_BlankDomain_WhiteFlash");
            TransitionFlashes[view.Pointer] = flashRoot;
            UnityEngine.Canvas canvas = flashRoot.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            UnityEngine.GameObject panel = new UnityEngine.GameObject(
                "White",
                new Il2CppSystem.Type[]
                {
                    Il2CppInterop.Runtime.Il2CppType.Of<UnityEngine.RectTransform>(),
                    Il2CppInterop.Runtime.Il2CppType.Of<UnityEngine.CanvasRenderer>(),
                    Il2CppInterop.Runtime.Il2CppType.Of<UnityEngine.UI.Image>()
                });
            panel.transform.SetParent(flashRoot.transform, false);
            UnityEngine.UI.Image image = panel.GetComponent<UnityEngine.UI.Image>();
            UnityEngine.RectTransform rect = image.rectTransform;
            rect.anchorMin = UnityEngine.Vector2.zero;
            rect.anchorMax = UnityEngine.Vector2.one;
            rect.offsetMin = UnityEngine.Vector2.zero;
            rect.offsetMax = UnityEngine.Vector2.zero;
            image.color = new UnityEngine.Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;

            Sequence sequence = DOTween.Sequence().SetUpdate(UpdateType.Late, true);
            TransitionSequences[view.Pointer] = sequence;
            // Finish the entire white prelude before starting either the
            // character timeline or its FX. The late pose driver also waits
            // for Started, so it cannot show motion under the fading white.
            sequence.Append(DOTween.To(
                (DG.Tweening.Core.DOGetter<float>)(() =>
                    TransitionClocks.TryGetValue(appearance.Pointer, out var clock) ? clock.PreludeTime : 0f),
                (DG.Tweening.Core.DOSetter<float>)(value => AdvanceWhitePrelude(appearance, image, value)),
                TransitionWhiteDuration, TransitionWhiteDuration).SetEase(Ease.Linear));
            sequence.AppendCallback((TweenCallback)(() => StartTransformationMotion(view, appearance, image)));
            // One linear source time drives both the native FX and poses.
            // The white image is disabled before this clock is allowed to run.
            sequence.Append(DOTween.To(
                (DG.Tweening.Core.DOGetter<float>)(() =>
                    TransitionClocks.TryGetValue(appearance.Pointer, out var clock) ? clock.Time : 0f),
                (DG.Tweening.Core.DOSetter<float>)(value => AdvanceTransformation(view, appearance, value)),
                TransitionSourceDuration,
                TransitionSourceDuration / TransitionPlaybackRate).SetEase(Ease.Linear));
            bool completed = false;
            sequence.OnComplete((TweenCallback)(() =>
            {
                completed = true;
                ReleaseTransitionPresentation(view, appearance, flashRoot);
            }));
            sequence.OnKill((TweenCallback)(() =>
            {
                if (!completed)
                {
                    // Stage cleanup kills owned transitions only to remove UI;
                    // it must not perform a late model swap into a dead battle.
                    if (ClearingTransitions)
                    {
                        ReleaseTransitionPresentation(view, appearance, flashRoot);
                        return;
                    }
                    // The permanent form flag and skill state were committed
                    // before this presentation began. If DOTween is killed
                    // during the prelude, finish the atomic model swap
                    // so visuals cannot remain in the old form.
                    Activate(view);
                    ReleaseTransitionPresentation(view, appearance, flashRoot);
                }
            }));
            Plugin.Logger?.LogInfo("Started Blank Domain: white prelude=1.10s, then detail 21 at rate=0.45 for 5.67s; no flash/motion overlap.");
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Blank Domain transition failed; applying model immediately: " + ex);
            Activate(view);
            ReleaseTransitionPresentation(view, appearance, flashRoot);
        }
    }

    private static void AdvanceWhitePrelude(SD.CharacterAppearance appearance,
        UnityEngine.UI.Image image, float elapsed)
    {
        if (appearance == null || !TransitionClocks.TryGetValue(appearance.Pointer, out var clock)) return;
        if (clock.Started) return;
        clock.PreludeTime = Math.Max(0f, Math.Min(TransitionWhiteDuration, elapsed));
        float t = clock.PreludeTime;
        float alpha = t < 0.18f ? UnityEngine.Mathf.SmoothStep(0f, 1f, t / 0.18f) :
            t < 0.35f ? 1f : UnityEngine.Mathf.SmoothStep(1f, 0f, (t - 0.35f) / 0.75f);
        if (image != null) image.color = new UnityEngine.Color(1f, 1f, 1f, alpha);
    }

    private static void StartTransformationMotion(BattleUnitView view,
        SD.CharacterAppearance appearance, UnityEngine.UI.Image image)
    {
        if (appearance == null || !IsTransforming(appearance) ||
            !TransitionClocks.TryGetValue(appearance.Pointer, out var clock) || clock.Started) return;
        if (image != null)
        {
            image.color = new UnityEngine.Color(1f, 1f, 1f, 0f);
            image.enabled = false;
        }
        clock.Started = true;
        SetIdleMode(appearance, false);
        PresentFrame(appearance, "transform_start");
        // Extract before rebuilding the graph and rebind afterwards. This
        // callback also runs after Lua's synchronous break recovery calls.
        BlankDomainActionEffectBindingAccess.EnsureTransformation(view);
        appearance.ChangeMotion((MOTION_DETAIL)21, true, 0, false, null, true);
        BlankDomainActionEffectBindingAccess.EnsureTransformation(view);
        clock.Director = appearance._playableDirector;
        clock.Timeline = clock.Director != null && clock.Director.playableAsset != null
            ? clock.Director.playableAsset.TryCast<UnityEngine.Timeline.TimelineAsset>() : null;
        if (clock.Director != null && clock.Timeline != null)
        {
            clock.PreviousMode = clock.Director.timeUpdateMode;
            clock.Director.timeUpdateMode = DirectorUpdateMode.Manual;
            clock.Director.time = 0;
            clock.Director.Evaluate();
        }
        BlankDomainActionEffectBindingAccess.SlowTransformationEffects(view, TransitionPlaybackRate);
        Plugin.Logger?.LogInfo("Blank Domain white prelude finished; starting character and FX together at rate=0.45.");
    }

    private static void AdvanceTransformation(BattleUnitView view,
        SD.CharacterAppearance appearance, float sourceTime)
    {
        if (appearance == null || !TransitionClocks.TryGetValue(appearance.Pointer, out var clock) ||
            !clock.Started) return;
        float previousTime = clock.Time;
        clock.Time = Math.Max(0f, Math.Min(TransitionSourceDuration, sourceTime));
        try
        {
            var director = clock.Director;
            if (director != null && clock.Timeline != null && director.playableAsset != null &&
                director.playableAsset.Pointer == clock.Timeline.Pointer)
            {
                director.timeUpdateMode = DirectorUpdateMode.Manual;
                director.time = clock.Time;
                director.Evaluate();
            }
            BlankDomainActionEffectBindingAccess.SlowTransformationEffects(view, TransitionPlaybackRate,
                Math.Max(0f, clock.Time - previousTime));
            if (!clock.Committed && clock.Time >= 0.90f)
            {
                clock.Committed = true;
                Activate(view, true);
            }
        }
        catch (Exception ex)
        {
            if (SampleDiagnostics.Add("transition-clock:" + appearance.Pointer))
                Plugin.Logger?.LogWarning("Blank Domain transition clock fallback: " + ex);
        }
    }

    private static void FinishTransformation(BattleUnitView view, SD.CharacterAppearance appearance)
    {
        if (appearance != null)
        {
            if (TransitionClocks.TryGetValue(appearance.Pointer, out var clock))
            {
                TransitionClocks.Remove(appearance.Pointer);
                if (clock.Director != null && clock.Timeline != null)
                {
                    try
                    {
                        if (clock.Director.playableAsset != null &&
                            clock.Director.playableAsset.Pointer == clock.Timeline.Pointer)
                            clock.Director.Stop();
                        clock.Director.timeUpdateMode = clock.PreviousMode;
                    }
                    catch (Exception ex) { Plugin.Logger?.LogWarning("Transition clock release: " + ex.Message); }
                }
            }
            TransformingAppearances.Remove(appearance.Pointer);
            TransformingViews.Remove(appearance.Pointer);
        }
        BlankDomainActionEffectBindingAccess.RemoveTransformation(view);
        if (!ClearingTransitions && appearance != null && IsActive(view))
            SetIdleMode(appearance, true);
    }

    private static void ReleaseTransitionPresentation(
        BattleUnitView view,
        SD.CharacterAppearance appearance,
        UnityEngine.GameObject flashRoot)
    {
        Plugin.Logger?.LogInfo("Blank Domain transition release begin.");
        FinishTransformation(view, appearance);
        if (!ClearingTransitions && view != null && !IsActive(view)) Activate(view);
        try
        {
            if (appearance != null)
                appearance.SetFocusing(false);
            if (view != null)
                view.SetFocusByCam(false);
            if (BattleCamManager.Instance != null)
                BattleCamManager.Instance.ResetFocus(false, false);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Blank Domain focus release failed: " + ex.Message);
        }
        if (view != null && view.Pointer != IntPtr.Zero)
        {
            TransitionSequences.Remove(view.Pointer);
            TransitionFlashes.Remove(view.Pointer);
        }
        if (flashRoot != null)
            UnityEngine.Object.Destroy(flashRoot);
        Plugin.Logger?.LogInfo("Blank Domain transition release complete.");
    }

    private static void Activate(BattleUnitView view, bool keepTransition = false)
    {
        if (view == null || view.Pointer == IntPtr.Zero || view.Appearance == null)
            return;
        SD.CharacterAppearance appearance = view.Appearance;
        if (!keepTransition) FinishTransformation(view, appearance);
        ActiveViewPointers.Add(view.Pointer);
        if (view.unitModel != null)
            ActiveModels.Add(view.unitModel.Pointer);
        ActiveAppearances.Add(appearance.Pointer);
        ActiveViews[appearance.Pointer] = view;
        EnsurePresentationTicker();
        ThumbActionEffectBindingAccess.Remove(view);
        GsoundVoiceMuteAccess.MuteAppearance(view);
        EnsureIdleRenderer(appearance);
        if (!keepTransition) SetIdleMode(appearance, true);
        EnsureSecondaryTracks(appearance, false);
        BlankDomainActionEffectBindingAccess.Ensure(view);
        BlankDomainSlotAccess.Replace(view.unitModel);
        EnsurePersistentShadowEffect(appearance);
        Plugin.Logger?.LogInfo("Blank Domain permanent model committed; transitionRunning=" + keepTransition + ".");
    }

    internal static bool IsActive(BattleUnitView view)
    {
        return view != null && view.Pointer != IntPtr.Zero &&
            view.unitModel != null && BlankDomainStateAccess.IsEntered(view.unitModel) &&
            ActiveModels.Contains(view.unitModel.Pointer);
    }

    internal static bool IsTransforming(SD.CharacterAppearance appearance)
    {
        return appearance != null && appearance.Pointer != IntPtr.Zero &&
            TransformingAppearances.Contains(appearance.Pointer);
    }

    internal static void Refresh(BattleUnitView view)
    {
        if (!IsActive(view) || view.Appearance == null)
            return;
        SD.CharacterAppearance appearance = view.Appearance;
        ReleaseReplacedPresentation(view, appearance.Pointer);
        ActiveViewPointers.Add(view.Pointer);
        ActiveAppearances.Add(appearance.Pointer);
        ActiveViews[appearance.Pointer] = view;
        ThumbActionEffectBindingAccess.Remove(view);
        bool showIdle;
        if (!IdleModes.TryGetValue(appearance.Pointer, out showIdle))
            showIdle = true;
        EnsureIdleRenderer(appearance);
        SetIdleMode(appearance, showIdle);
        EnsureSecondaryTracks(appearance, false);
        BlankDomainActionEffectBindingAccess.Ensure(view);
        EnsurePersistentShadowEffect(appearance);
    }

    private static UnityEngine.SpriteRenderer EnsureIdleRenderer(SD.CharacterAppearance appearance)
    {
        if (!EnsureRuntimeArtAlive("renderer creation")) return null;
        PresentationAppearances[appearance.Pointer] = appearance;
        UnityEngine.SpriteRenderer existing;
        if (IdleRenderers.TryGetValue(appearance.Pointer, out existing) && existing != null)
            return existing;
        if (_idleSprite == null || appearance.sprenderer_charactermotion == null)
            return null;

        UnityEngine.SpriteRenderer source = appearance.sprenderer_charactermotion;
        UnityEngine.Transform parent = appearance.transform;
        UnityEngine.GameObject go = new UnityEngine.GameObject(IdleObjectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = source.transform.localPosition;
        go.transform.localRotation = source.transform.localRotation;
        go.transform.localScale = source.transform.localScale;
        UnityEngine.SpriteRenderer renderer = go.AddComponent<UnityEngine.SpriteRenderer>();
        go.layer = source.gameObject.layer;
        renderer.sprite = _idleSprite;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder;
        renderer.flipX = source.flipX;
        renderer.flipY = source.flipY;
        renderer.enabled = false;
        IdleRenderers[appearance.Pointer] = renderer;
        return renderer;
    }

    internal static void OnMotionChanged(SD.CharacterAppearance appearance, MOTION_DETAIL detail)
    {
        BlankDomainActionEffectBindingAccess.PrepareCurrentGraph(appearance);
        if (!ResolveActiveAppearance(appearance))
            return;

        EnsurePresentationTicker();

        BattleUnitView view;
        if (ActiveViews.TryGetValue(appearance.Pointer, out view) && view != null)
            BlankDomainActionEffectBindingAccess.Ensure(view);

        if (detail != MOTION_DETAIL.Default && detail != MOTION_DETAIL.Idle && detail != MOTION_DETAIL.UnRetreat)
            RecoveryStarts.Remove(appearance.Pointer);
        else if (PresentRecovery(appearance)) return;
        int value = (int)detail;
        bool customAttack = value == 14 || value == 15 || value == 16 ||
            value == 24 || value == 25;
        if (customAttack)
            EnsureSecondaryTracks(appearance, false);
        SetIdleMode(appearance, !customAttack);
        if (customAttack) SampleCurrentFrame(appearance);
        var director = appearance._playableDirector;
        string timeline = director != null && director.playableAsset != null
            ? director.playableAsset.name : "<none>";
        string diagnostic = appearance.Pointer + ":" + value + ":" + timeline;
        if (MotionDiagnostics.Add(diagnostic))
            Plugin.Logger?.LogInfo("Blank Domain motion " + value + " -> " + timeline +
                "; native spine suppressed, idle=" + !customAttack + ".");
    }

    private static bool ResolveActiveAppearance(SD.CharacterAppearance appearance)
    {
        if (appearance == null || appearance.Pointer == IntPtr.Zero ||
            IsTransforming(appearance))
            return false;
        BattleUnitView view = appearance._battleUnitView;
        if (!IsActive(view))
            return false;
        if (!ActiveAppearances.Contains(appearance.Pointer))
        {
            Refresh(view);
            Plugin.Logger?.LogInfo("Blank Domain restored permanent form on refreshed appearance.");
        }
        return true;
    }

    internal static void MaintainPresentation(SD.CharacterAppearance appearance)
    {
        if (!ResolveActiveAppearance(appearance))
            return;
        EnsurePersistentShadowEffect(appearance);
        if (PresentRecovery(appearance)) return;
        // Native round/duel callbacks can change state without ChangeMotion.
        int detail = (int)appearance._currentMotiondetail;
        bool showIdle = detail != 14 && detail != 15 && detail != 16 &&
            detail != 24 && detail != 25;
        SetIdleMode(appearance, showIdle);
    }

    internal static void UpdatePresentation(SD.CharacterAppearance appearance)
    {
        if (!OwnsPresentation(appearance)) return;
        if (IsTransforming(appearance)) return; // Late driver owns explicit transition time.
        MaintainPresentation(appearance);
        bool idle;
        if (IdleModes.TryGetValue(appearance.Pointer, out idle) && !idle)
            SampleCurrentFrame(appearance);
    }

    internal static bool RemapMotion(SD.CharacterAppearance appearance, ref MOTION_DETAIL detail)
    {
        if (appearance == null || appearance.Pointer == IntPtr.Zero)
            return true;
        if (TransformingAppearances.Contains(appearance.Pointer))
        {
            int requested = (int)detail;
            if (requested == 21)
                return true;
            Plugin.Logger?.LogInfo("Blank Domain transform skipped interrupting motion " + requested + ".");
            return false;
        }
        if (!ResolveActiveAppearance(appearance))
            return true;
        BattleUnitView view;
        if (ActiveViews.TryGetValue(appearance.Pointer, out view) && view != null)
            BlankDomainActionEffectBindingAccess.Ensure(view);
        // Parrying (17) is shared by both forms. Redirect it to the private
        // Blank Domain parry slot only after the permanent model swap.
        if (detail == MOTION_DETAIL.Parrying || detail == MOTION_DETAIL.Parrying_Range ||
            detail == MOTION_DETAIL.Parrying_Lose)
        {
            detail = (MOTION_DETAIL)25;
            if (ParryRemapLogged.Add(appearance.Pointer))
                Plugin.Logger?.LogInfo("Blank Domain clash motion remapped to private detail 25.");
        }
        return true;
    }

    // Movement has no private attack Timeline. Keep native movement and its
    // arrival callback, but present the grounded, forward-leaning greatsword pose.
    internal static string StaticMotionFrame(MOTION_DETAIL detail)
    {
        switch (detail)
        {
            case MOTION_DETAIL.Move: return "slash";
            case MOTION_DETAIL.Duel_Ready:
            case MOTION_DETAIL.Duel_Ready_Actor:
            case MOTION_DETAIL.Duel_Ready_Target: return "windup";
            default: return "idle";
        }
    }

    private static void SetIdleMode(SD.CharacterAppearance appearance, bool showIdle)
    {
        SuppressDefaultSpine(appearance);
        IdleModes[appearance.Pointer] = showIdle;
        UnityEngine.SpriteRenderer idle = EnsureIdleRenderer(appearance);
        bool replacementVisible = idle != null;
        if (idle != null)
        {
            UnityEngine.SpriteRenderer source = appearance.sprenderer_charactermotion;
            if (source != null)
            {
                UnityEngine.Transform desiredParent = appearance.transform;
                if (idle.transform.parent != desiredParent)
                    idle.transform.SetParent(desiredParent, false);
                // Follow the animation transform without inheriting an inactive
                // Sprite/Spine switching branch. Battle-root hiding still applies.
                idle.transform.position = source.transform.position;
                idle.transform.rotation = source.transform.rotation;
                var worldScale = source.transform.lossyScale;
                var parentScale = desiredParent.lossyScale;
                idle.transform.localScale = new UnityEngine.Vector3(
                    Math.Abs(parentScale.x) > 0.0001f ? worldScale.x / parentScale.x : 1f,
                    Math.Abs(parentScale.y) > 0.0001f ? worldScale.y / parentScale.y : 1f,
                    Math.Abs(parentScale.z) > 0.0001f ? worldScale.z / parentScale.z : 1f);
                idle.gameObject.layer = source.gameObject.layer;
                idle.flipX = source.flipX;
                idle.flipY = source.flipY;
                idle.sortingLayerID = source.sortingLayerID;
                idle.sortingOrder = source.sortingOrder;
            }
            if (showIdle)
            {
                string frame = StaticMotionFrame(appearance._currentMotiondetail);
                idle.sprite = RuntimeFrames.TryGetValue(frame, out var pose) && pose != null
                    ? pose : _idleSprite;
                idle.color = UnityEngine.Color.white;
            }
            idle.enabled = replacementVisible;
            idle.forceRenderingOff = false;
            if (!idle.gameObject.activeSelf) idle.gameObject.SetActive(true);
        }
        // Never hide the original character unless the permanent replacement
        // renderer actually exists. A failed art lookup must degrade to the
        // old model rather than making Gsound disappear after the white flash.
        SetOriginalVisible(appearance, !replacementVisible);
        string checkKey = appearance.Pointer + ":" + (int)appearance._currentMotiondetail + ":" +
            (idle != null && idle.gameObject.activeInHierarchy);
        if (PresentationChecks.Add(checkKey))
            Plugin.Logger?.LogInfo("Blank Domain presentation check: detail=" + (int)appearance._currentMotiondetail +
                "; replacementActive=" + (idle != null && idle.gameObject.activeInHierarchy) +
                "; staticFrame=" + (showIdle ? StaticMotionFrame(appearance._currentMotiondetail) : "timeline") +
                "; disableSpine=" + appearance._isDisableSpine + "; view=" +
                (appearance._battleUnitView != null ? appearance._battleUnitView.Pointer : IntPtr.Zero));
        SetSecondaryTracksVisible(appearance, !showIdle);
        // Idle / round / duel presentation can re-enable the donor's Spine
        // mesh independently of the SpriteRenderer used by action timelines.
        // Keep that separate renderer hidden for the whole committed form.
        if (idle != null)
        {
            var skins = appearance._spineSkins;
            if (skins != null)
            {
                for (int i = 0; i < skins.Count; i++)
                {
                    var mesh = skins[i] != null ? skins[i]._spine_renderer : null;
                    if (mesh == null)
                        continue;
                    if (!HiddenSpineRenderers.ContainsKey(mesh.Pointer))
                    {
                        HiddenSpineRenderers[mesh.Pointer] = mesh;
                        OriginalSpineRenderingOff[mesh.Pointer] = mesh.forceRenderingOff;
                    }
                    mesh.forceRenderingOff = true;
                }
            }
            if (!showIdle && appearance.sprenderer_charactermotion != null)
                appearance.sprenderer_charactermotion.enabled = true;
        }
    }

    private static void SetOriginalVisible(SD.CharacterAppearance appearance, bool visible)
    {
        if (appearance.sprenderer_charactermotion != null)
            appearance.sprenderer_charactermotion.forceRenderingOff = !visible;
        var parts = appearance.Sprenderer_charactermotion_Parts;
        if (parts == null)
            return;
        for (int index = 0; index < parts.Count; index++)
        {
            if (parts[index] != null)
                parts[index].forceRenderingOff = !visible;
        }
    }

    private const string SampleBodyPath =
        "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[SpRenderer]";
    private static readonly string[] SamplePartNames =
        { SecondaryTrailName, SecondaryTrail2Name, SecondaryHorseName, SecondaryCoffinName, SecondarySlashName };
    private sealed class FrameSampler
    {
        internal UnityEngine.GameObject Root;
        internal UnityEngine.SpriteRenderer Body;
        internal UnityEngine.SpriteRenderer[] Parts;
        internal UnityEngine.Vector3[] PartPositions;
        internal UnityEngine.Quaternion[] PartRotations;
        internal UnityEngine.Vector3[] PartScales;
        internal IntPtr Timeline;
        internal readonly List<UnityEngine.Timeline.TimelineClip> Clips =
            new List<UnityEngine.Timeline.TimelineClip>();
    }
    private static readonly Dictionary<IntPtr, FrameSampler> FrameSamplers =
        new Dictionary<IntPtr, FrameSampler>();
    private static readonly HashSet<string> SampleDiagnostics = new HashSet<string>();

    private static UnityEngine.Transform MakeSamplePath(UnityEngine.Transform root, string path)
    {
        foreach (string name in path.Split('/'))
        {
            var child = root.Find(name);
            if (child == null)
            {
                child = new UnityEngine.GameObject(name).transform;
                child.SetParent(root, false);
            }
            root = child;
        }
        return root;
    }

    // Called after Unity's animation update. The proxy has only Transforms and
    // hidden SpriteRenderers: sampling cannot move a battle unit or fire its
    // Timeline damage/FX callbacks. The native director still controls those.
    internal static void LateUpdatePresentation()
    {
        foreach (var entry in new List<KeyValuePair<IntPtr, BattleUnitView>>(TransformingViews))
        {
            try
            {
                var appearance = entry.Value != null ? entry.Value.Appearance : null;
                if (appearance == null || !IsTransforming(appearance) ||
                    !TransitionClocks.TryGetValue(entry.Key, out var clock) || !clock.Started) continue;
                var motion = appearance.GetMotion((MOTION_DETAIL)21, false);
                if (motion == null || motion.timelineAssets == null || motion.timelineAssets.Count == 0) continue;
                // Own the character presentation without importing the donor's
                // horse rig or moving the live battle root with SampleAnimation.
                SetIdleMode(appearance, false);
                SampleCurrentFrame(appearance, motion.timelineAssets[0], clock.Time);
                BlankDomainActionEffectBindingAccess.SlowTransformationEffects(entry.Value, TransitionPlaybackRate);
            }
            catch (Exception ex)
            {
                if (SampleDiagnostics.Add("transform-error:" + entry.Key))
                    Plugin.Logger?.LogWarning("Blank Domain transformation frame failed: " + ex);
            }
        }
        foreach (var registeredView in new List<BattleUnitView>(ActiveViews.Values))
        {
            try
            {
                var model = registeredView != null ? registeredView.unitModel : null;
                var view = model != null && BattleObjectManager.Instance != null
                    ? BattleObjectManager.Instance.GetView(model) : registeredView;
                if (!IsActive(view) || view.Appearance == null || IsTransforming(view.Appearance))
                    continue;
                var appearance = view.Appearance;
                if (SampleDiagnostics.Add("tick:" + appearance.Pointer))
                    Plugin.Logger?.LogInfo("Blank Domain late presentation running: " + appearance.Pointer);
                MaintainPresentation(appearance);
                bool idleMode;
                if (!IdleModes.TryGetValue(appearance.Pointer, out idleMode) || idleMode)
                    continue;
                SampleCurrentFrame(appearance);
            }
            catch (Exception ex)
            {
                if (SampleDiagnostics.Add("error:" + (registeredView != null ? registeredView.Pointer : IntPtr.Zero)))
                    Plugin.Logger?.LogWarning("Blank Domain frame presentation failed: " + ex);
            }
        }
    }

    private static bool PresentAuthoredFrame(SD.CharacterAppearance appearance,
        UnityEngine.Timeline.TimelineAsset timeline, double time, bool transforming)
    {
        int detail = transforming ? 21 : (int)appearance._currentMotiondetail;
        string kind = detail == 14 ? "skill1" : detail == 15 ? "skill2" :
            detail == 16 ? "skill3" : detail == 24 ? "counter" :
            detail == 25 ? "parry" : detail == 21 ? "transform" : null;
        if (kind == null) return false;
        UnityEngine.Timeline.TimelineClip selected = null;
        double firstStart = double.MaxValue;
        foreach (var track in timeline.flattenedTracks)
        {
            if (track == null || track.TryCast<UnityEngine.Timeline.AnimationTrack>() == null) continue;
            foreach (var clip in track.clips)
            {
                var asset = clip.asset != null ? clip.asset.TryCast<UnityEngine.Timeline.AnimationPlayableAsset>() : null;
                if (asset == null || asset.clip == null || !AuthoredFrames.ContainsKey(kind + "|" + asset.clip.name)) continue;
                if (selected == null || (clip.start <= time && (selected.start > time || clip.start >= selected.start)) ||
                    (selected.start > time && clip.start < firstStart)) selected = clip;
                firstStart = Math.Min(firstStart, clip.start);
            }
        }
        if (selected == null) return false;
        var animation = selected.asset.TryCast<UnityEngine.Timeline.AnimationPlayableAsset>().clip;
        var keys = AuthoredFrames[kind + "|" + animation.name];
        double local = Math.Max(0, Math.Min(animation.length,
            selected.ToLocalTime(Math.Max(selected.start, Math.Min(time, selected.end)))));
        string frame = keys[0].Frame;
        foreach (var key in keys)
            if (key.Time <= local + 0.000001) frame = key.Frame;
        UnityEngine.Sprite sprite;
        if (!RuntimeFrames.TryGetValue(frame, out sprite) || sprite == null) return false;
        var renderer = EnsureIdleRenderer(appearance);
        if (renderer == null) return false;
        renderer.sprite = sprite;
        renderer.color = UnityEngine.Color.white;
        renderer.enabled = true;
        renderer.forceRenderingOff = false;
        // These are an original donor's character/horse layers, not detached
        // particle FX. The redrawn grounded character replaces that silhouette.
        var body = appearance.sprenderer_charactermotion;
        if (body != null)
            foreach (string name in new[] { SecondaryHorseName, SecondaryCoffinName })
            {
                var part = body.transform.Find(name);
                var partRenderer = part != null ? part.GetComponent<UnityEngine.SpriteRenderer>() : null;
                if (partRenderer != null) partRenderer.forceRenderingOff = true;
            }
        if (SampleDiagnostics.Add("authored:" + appearance.Pointer + ":" + timeline.Pointer))
            Plugin.Logger?.LogInfo("Blank Domain authored frame: " + kind + "/" + animation.name +
                " -> " + frame + "; native timing, permanent renderer active.");
        return true;
    }

    private static void SampleCurrentFrame(SD.CharacterAppearance appearance,
        UnityEngine.Timeline.TimelineAsset forcedTimeline = null, double? forcedTime = null)
    {
        if (forcedTimeline == null && PresentRecovery(appearance)) return;
        if (!EnsureRuntimeArtAlive("frame sampling")) return;
        var director = appearance._playableDirector;
        var timeline = forcedTimeline != null ? forcedTimeline :
            (director != null && director.playableAsset != null
            ? director.playableAsset.TryCast<UnityEngine.Timeline.TimelineAsset>() : null);
        double presentationTime = forcedTime ?? (director != null ? director.time : 0);
        if (timeline != null)
        {
            int detail = forcedTimeline != null ? 21 : (int)appearance._currentMotiondetail;
            string kind = detail == 14 ? "skill1" : detail == 15 ? "skill2" : detail == 16 ? "skill3" :
                detail == 24 ? "counter" : detail == 25 ? "parry" : detail == 21 ? "transform" : "";
            string key = kind + "|" + timeline.name;
            if (TimelineFrames.TryGetValue(key, out var events) && events.Count > 0)
            {
                string frame = events[0].Frame;
                foreach (var e in events) if (e.Time <= presentationTime + 0.000001) frame = e.Frame;
                if (RuntimeFrames.TryGetValue(frame, out var sprite) && sprite != null)
                {
                    var renderer = EnsureIdleRenderer(appearance);
                    if (renderer != null)
                    {
                        renderer.sprite = sprite;
                        renderer.color = UnityEngine.Color.white;
                        renderer.enabled = true;
                        renderer.forceRenderingOff = false;
                        // The native donor body is suppressed; keep its mount layers hidden too.
                        var body = appearance.sprenderer_charactermotion;
                        if (body != null)
                            foreach (string partName in new[] { SecondaryHorseName, SecondaryCoffinName })
                            {
                                var part = body.transform.Find(partName);
                                var r = part != null ? part.GetComponent<UnityEngine.SpriteRenderer>() : null;
                                if (r != null) r.forceRenderingOff = true;
                            }
                        if (SampleDiagnostics.Add("direct:" + appearance.Pointer + key + ":" + frame))
                            Plugin.Logger?.LogInfo("Blank Domain direct frame: " + key + " -> " + frame +
                                "; t=" + presentationTime.ToString("F3") + "; active=" + renderer.gameObject.activeInHierarchy);
                        return;
                    }
                }
            }
            else if (SampleDiagnostics.Add("unmapped:" + key))
                Plugin.Logger?.LogWarning("Blank Domain unmapped presentation timeline: " + key);
        }
        if (!BlankDomainActionEffectBindingAccess.IsPrivateTimeline(appearance, timeline))
        {
            SetIdleMode(appearance, true);
            return;
        }
        if (PresentAuthoredFrame(appearance, timeline, presentationTime, forcedTimeline != null))
            return;
        var output = EnsureIdleRenderer(appearance);
        if (output == null)
            return;
        FrameSampler sampler;
        if (!FrameSamplers.TryGetValue(appearance.Pointer, out sampler) || sampler.Root == null)
        {
            sampler = new FrameSampler();
            sampler.Root = new UnityEngine.GameObject("Gsound_BlankDomain_FrameSampler");
            sampler.Root.transform.SetParent(appearance.transform, false);
            sampler.Body = MakeSamplePath(sampler.Root.transform, SampleBodyPath)
                .gameObject.AddComponent<UnityEngine.SpriteRenderer>();
            sampler.Body.forceRenderingOff = true;
            sampler.Parts = new UnityEngine.SpriteRenderer[SamplePartNames.Length];
            sampler.PartPositions = new UnityEngine.Vector3[SamplePartNames.Length];
            sampler.PartRotations = new UnityEngine.Quaternion[SamplePartNames.Length];
            sampler.PartScales = new UnityEngine.Vector3[SamplePartNames.Length];
            for (int i = 0; i < SamplePartNames.Length; i++)
            {
                sampler.Parts[i] = MakeSamplePath(sampler.Body.transform, SamplePartNames[i])
                    .gameObject.AddComponent<UnityEngine.SpriteRenderer>();
                sampler.Parts[i].forceRenderingOff = true;
                var sourcePart = appearance.sprenderer_charactermotion.transform.Find(SamplePartNames[i]);
                sampler.PartPositions[i] = sourcePart != null ? sourcePart.localPosition : UnityEngine.Vector3.zero;
                sampler.PartRotations[i] = sourcePart != null ? sourcePart.localRotation : UnityEngine.Quaternion.identity;
                sampler.PartScales[i] = sourcePart != null ? sourcePart.localScale : UnityEngine.Vector3.one;
            }
            FrameSamplers[appearance.Pointer] = sampler;
        }
        if (sampler.Timeline != timeline.Pointer)
        {
            sampler.Timeline = timeline.Pointer;
            sampler.Clips.Clear();
            foreach (var track in timeline.flattenedTracks)
            {
                if (track == null || track.TryCast<UnityEngine.Timeline.AnimationTrack>() == null)
                    continue;
                foreach (var clip in track.clips)
                    if (clip.asset != null && clip.asset.TryCast<UnityEngine.Timeline.AnimationPlayableAsset>() != null)
                        sampler.Clips.Add(clip);
            }
            sampler.Clips.Sort((a, b) => a.start.CompareTo(b.start));
        }
        UnityEngine.Timeline.TimelineClip selected = null;
        foreach (var clip in sampler.Clips)
        {
            if (selected == null || clip.start <= presentationTime)
                selected = clip;
        }
        if (selected == null)
        {
            SetIdleMode(appearance, true);
            if (SampleDiagnostics.Add("missing:" + timeline.Pointer))
                Plugin.Logger?.LogWarning("Blank Domain timeline has no sampleable animation: " + timeline.name);
            return;
        }
        var animation = selected.asset.TryCast<UnityEngine.Timeline.AnimationPlayableAsset>().clip;
        if (animation == null)
            return;
        double time = Math.Max(selected.start, Math.Min(presentationTime, selected.end));
        // ToLocalTime retains the authored clipIn/timeScale. Hold the end frame
        // during the remaining camera/FX tail instead of exposing the old form.
        float localTime = (float)Math.Max(0, Math.Min(animation.length, selected.ToLocalTime(time)));
        sampler.Body.sprite = _idleSprite;
        sampler.Body.enabled = true;
        sampler.Body.color = UnityEngine.Color.white;
        for (int i = 0; i < sampler.Parts.Length; i++)
        {
            var part = sampler.Parts[i];
            part.sprite = null;
            part.color = UnityEngine.Color.white;
            part.enabled = true;
            part.transform.localPosition = sampler.PartPositions[i];
            part.transform.localRotation = sampler.PartRotations[i];
            part.transform.localScale = sampler.PartScales[i];
        }
        animation.SampleAnimation(sampler.Root, localTime);
        output.sprite = sampler.Body.sprite;
        output.color = sampler.Body.color;
        output.enabled = sampler.Body.enabled;
        // These redrawn frames share Gsound's facing direction. Keep the live
        // body's transform (already copied by SetIdleMode), rather than the
        // Maou prefab's extra X reflection. Selected clips have no body-local
        // Transform curve; only secondary layers need sampled local transforms.
        var source = appearance.sprenderer_charactermotion;
        for (int i = 0; source != null && i < SamplePartNames.Length; i++)
        {
            var partTransform = source.transform.Find(SamplePartNames[i]);
            var part = partTransform != null ? partTransform.GetComponent<UnityEngine.SpriteRenderer>() : null;
            if (part == null)
                continue;
            part.sprite = sampler.Parts[i].sprite;
            part.color = sampler.Parts[i].color;
            part.enabled = sampler.Parts[i].enabled;
            part.transform.localPosition = sampler.Parts[i].transform.localPosition;
            part.transform.localRotation = sampler.Parts[i].transform.localRotation;
            part.transform.localScale = sampler.Parts[i].transform.localScale;
        }
        if (SampleDiagnostics.Add("sample:" + appearance.Pointer + ":" + timeline.Pointer))
            Plugin.Logger?.LogInfo("Blank Domain sampled frame: " + timeline.name + " / " + animation.name +
                " -> " + (output.sprite != null ? output.sprite.name : "<hidden frame>") +
                "; old character renderer suppressed.");
    }

    private static void ReleaseReplacedPresentation(BattleUnitView view, IntPtr current)
    {
        var obsolete = new List<IntPtr>();
        foreach (var pair in ActiveViews)
            if (pair.Key != current && pair.Value != null &&
                (pair.Value.Pointer == view.Pointer ||
                 (pair.Value.unitModel != null && view.unitModel != null &&
                  pair.Value.unitModel.Pointer == view.unitModel.Pointer)))
                obsolete.Add(pair.Key);
        foreach (var pointer in obsolete)
        {
            SD.CharacterAppearance previous;
            if (PresentationAppearances.TryGetValue(pointer, out previous) && previous != null)
            {
                SetOriginalVisible(previous, true);
                if (SavedDisableSpine.TryGetValue(pointer, out var disabled))
                    previous.SetDisableSpine(disabled);
            }
            SavedDisableSpine.Remove(pointer);
            UnityEngine.SpriteRenderer idle;
            if (IdleRenderers.TryGetValue(pointer, out idle) && idle != null)
                UnityEngine.Object.Destroy(idle.gameObject);
            FrameSampler sampler;
            if (FrameSamplers.TryGetValue(pointer, out sampler) && sampler.Root != null)
                UnityEngine.Object.Destroy(sampler.Root);
            List<UnityEngine.GameObject> parts;
            if (SecondaryTrackObjects.TryGetValue(pointer, out parts))
                foreach (var part in parts)
                    if (part != null)
                        UnityEngine.Object.Destroy(part);
            RecoveryStarts.Remove(pointer);
            DestroyPersistentShadowEffect(pointer);
            ActiveViews.Remove(pointer);
            ActiveAppearances.Remove(pointer);
            PresentationAppearances.Remove(pointer);
            IdleRenderers.Remove(pointer);
            IdleModes.Remove(pointer);
            FrameSamplers.Remove(pointer);
            SecondaryTrackObjects.Remove(pointer);
            SecondaryHorseWildHunt.Remove(pointer);
            ParryRemapLogged.Remove(pointer);
        }
    }

    internal static void Clear()
    {
        foreach (var pair in PresentationAppearances)
            if (pair.Value != null && SavedDisableSpine.TryGetValue(pair.Key, out var disabled))
                pair.Value.SetDisableSpine(disabled);
        SavedDisableSpine.Clear();
        PresentationChecks.Clear();
        if (PresentationTicker != null)
        {
            PresentationTicker.Kill(false);
            PresentationTicker = null;
        }
        foreach (var sampler in FrameSamplers.Values)
            if (sampler.Root != null)
                UnityEngine.Object.Destroy(sampler.Root);
        FrameSamplers.Clear();
        SampleDiagnostics.Clear();
        ClearingTransitions = true;
        try
        {
            var sequences = new List<Sequence>(TransitionSequences.Values);
            for (int index = 0; index < sequences.Count; index++)
            {
                if (sequences[index] != null && sequences[index].IsActive())
                    sequences[index].Kill(false);
            }
            foreach (UnityEngine.GameObject flash in TransitionFlashes.Values)
            {
                if (flash != null)
                    UnityEngine.Object.Destroy(flash);
            }
        }
        finally
        {
            TransitionSequences.Clear();
            TransitionFlashes.Clear();
            TransformingAppearances.Clear();
            TransformingViews.Clear();
            TransitionClocks.Clear();
            ClearingTransitions = false;
        }
        // A view may survive a stage-manager reset long enough to be reused by
        // a result/retry flow. Explicitly release forceRenderingOff before the
        // replacement renderers and pointer registries are discarded.
        foreach (BattleUnitView view in ActiveViews.Values)
        {
            if (view != null && view.Appearance != null)
                SetOriginalVisible(view.Appearance, true);
        }
        foreach (UnityEngine.SpriteRenderer renderer in IdleRenderers.Values)
        {
            if (renderer != null && renderer.gameObject != null)
                UnityEngine.Object.Destroy(renderer.gameObject);
        }
        IdleRenderers.Clear();
        IdleModes.Clear();
        foreach (List<UnityEngine.GameObject> owned in SecondaryTrackObjects.Values)
        {
            if (owned == null)
                continue;
            for (int index = 0; index < owned.Count; index++)
            {
                if (owned[index] != null)
                    UnityEngine.Object.Destroy(owned[index]);
            }
        }
        SecondaryTrackObjects.Clear();
        SecondaryHorseWildHunt.Clear();
        foreach (IntPtr pointer in new List<IntPtr>(PersistentShadowEffects.Keys))
            DestroyPersistentShadowEffect(pointer);
        PersistentShadowEffects.Clear();
        RecoveryStarts.Clear();
        PersistentShadowRetryAt = 0f;
        PersistentShadowMissingLogged = false;
        ActiveAppearances.Clear();
        PresentationAppearances.Clear();
        ActiveViewPointers.Clear();
        ActiveViews.Clear();
        ParryRemapLogged.Clear();
        ActiveModels.Clear();
        MotionDiagnostics.Clear();
        foreach (var mesh in HiddenSpineRenderers.Values)
            if (mesh != null)
                mesh.forceRenderingOff = OriginalSpineRenderingOff[mesh.Pointer];
        HiddenSpineRenderers.Clear();
        OriginalSpineRenderingOff.Clear();
    }

    // Encounter reset is distinct from StageBuffManager.Clear, which the game
    // calls at every round boundary. Keep the committed visual form through a
    // round clear; remove it only when Lua explicitly resets this model.
    internal static void ResetModel(BattleUnitModel model)
    {
        if (model == null || model.Pointer == IntPtr.Zero)
            return;
        var matches = new List<IntPtr>();
        foreach (var pair in ActiveViews)
            if (pair.Value != null && pair.Value.unitModel != null &&
                pair.Value.unitModel.Pointer == model.Pointer)
                matches.Add(pair.Key);
        foreach (IntPtr pointer in matches)
        {
            BattleUnitView matchedView;
            if (ActiveViews.TryGetValue(pointer, out matchedView) && matchedView != null)
                ActiveViewPointers.Remove(matchedView.Pointer);
            SD.CharacterAppearance appearance;
            PresentationAppearances.TryGetValue(pointer, out appearance);
            if (appearance != null)
            {
                SetOriginalVisible(appearance, true);
                if (SavedDisableSpine.TryGetValue(pointer, out var disabled))
                    appearance.SetDisableSpine(disabled);
                var skins = appearance._spineSkins;
                if (skins != null)
                    for (int i = 0; i < skins.Count; i++)
                    {
                        var mesh = skins[i] != null ? skins[i]._spine_renderer : null;
                        if (mesh != null && OriginalSpineRenderingOff.ContainsKey(mesh.Pointer))
                            mesh.forceRenderingOff = OriginalSpineRenderingOff[mesh.Pointer];
                    }
            }
            SavedDisableSpine.Remove(pointer);
            UnityEngine.SpriteRenderer idle;
            if (IdleRenderers.TryGetValue(pointer, out idle) && idle != null)
                UnityEngine.Object.Destroy(idle.gameObject);
            FrameSampler sampler;
            if (FrameSamplers.TryGetValue(pointer, out sampler) && sampler.Root != null)
                UnityEngine.Object.Destroy(sampler.Root);
            List<UnityEngine.GameObject> secondary;
            if (SecondaryTrackObjects.TryGetValue(pointer, out secondary) && secondary != null)
                foreach (var go in secondary)
                    if (go != null) UnityEngine.Object.Destroy(go);
            RecoveryStarts.Remove(pointer);
            DestroyPersistentShadowEffect(pointer);
            ActiveViews.Remove(pointer);
            ActiveAppearances.Remove(pointer);
            PresentationAppearances.Remove(pointer);
            IdleRenderers.Remove(pointer);
            IdleModes.Remove(pointer);
            FrameSamplers.Remove(pointer);
            SecondaryTrackObjects.Remove(pointer);
            SecondaryHorseWildHunt.Remove(pointer);
            ParryRemapLogged.Remove(pointer);
        }
        ActiveModels.Remove(model.Pointer);
        if (matches.Count > 0)
            Plugin.Logger?.LogInfo("Blank Domain encounter reset released persistent form for model " + model.Pointer + ".");
    }

    private static void EnsurePersistentShadowEffect(SD.CharacterAppearance appearance)
    {
        if (appearance == null || appearance.Pointer == IntPtr.Zero) return;
        if (PersistentShadowEffects.TryGetValue(appearance.Pointer, out var existing) && existing != null)
        {
            if (!existing.activeSelf) { existing.SetActive(true); PlayPersistentShadowParticles(existing); }
            else if (UnityEngine.Time.unscaledTime >= PersistentShadowHealthAt)
            {
                PersistentShadowHealthAt = UnityEngine.Time.unscaledTime + 0.5f;
                PlayPersistentShadowParticles(existing);
            }
            return;
        }
        if (UnityEngine.Time.unscaledTime < PersistentShadowRetryAt) return;
        UnityEngine.GameObject clone = null;
        try
        {
            var parent = FindPresentationDescendant(appearance.transform, "[Transform]DefaultEffectPivot") ?? appearance.transform;
            clone = Plugin.InstantiateBlankDomainDonor("GsoundBlankShadow.bundle", "assets/gsound/blacknightmare.prefab", parent);
            if (clone == null) throw new InvalidDataException("Private BlackNightmare prefab unavailable");
            clone.name = PersistentShadowObjectName;
            clone.transform.localPosition = UnityEngine.Vector3.zero;
            clone.transform.localRotation = UnityEngine.Quaternion.identity;
            // The source scene used 2.2 scale, including its enemy-facing sign.
            // Facing is inherited from Gsound's own appearance instead.
            clone.transform.localScale = UnityEngine.Vector3.one * 2.2f;
            var body = appearance.sprenderer_charactermotion;
            foreach (var child in clone.GetComponentsInChildren<UnityEngine.Transform>(true))
                if (child != null) child.gameObject.layer = body != null ? body.gameObject.layer : appearance.gameObject.layer;
            foreach (var renderer in clone.GetComponentsInChildren<UnityEngine.ParticleSystemRenderer>(true))
            {
                if (renderer == null) continue;
                renderer.enabled = true;
                renderer.forceRenderingOff = false;
                if (body != null) { renderer.sortingLayerID = body.sortingLayerID; renderer.sortingOrder = body.sortingOrder - 1; }
            }
            clone.SetActive(true);
            PlayPersistentShadowParticles(clone);
            PersistentShadowEffects[appearance.Pointer] = clone;
            PersistentShadowMissingLogged = false;
            Plugin.Logger?.LogInfo("Blank Domain private BlackNightmare attached; appearance=" + appearance.Pointer);
        }
        catch (Exception ex)
        {
            if (clone != null) UnityEngine.Object.Destroy(clone);
            PersistentShadowRetryAt = UnityEngine.Time.unscaledTime + 2f;
            if (!PersistentShadowMissingLogged) Plugin.Logger?.LogWarning("Blank Domain private shadow: " + ex.Message);
            PersistentShadowMissingLogged = true;
        }
    }



    private static void PlayPersistentShadowParticles(UnityEngine.GameObject root)
    {
        if (root == null)
            return;
        UnityEngine.ParticleSystem[] particles =
            root.GetComponentsInChildren<UnityEngine.ParticleSystem>(true);
        if (particles == null)
            return;
        for (int index = 0; index < particles.Length; index++)
        {
            UnityEngine.ParticleSystem particle = particles[index];
            if (particle == null)
                continue;
            particle.gameObject.SetActive(true);
            if (!particle.isPlaying) particle.Play(false);
        }
    }

    private static void DestroyPersistentShadowEffect(IntPtr appearancePointer)
    {
        UnityEngine.GameObject effect;
        if (!PersistentShadowEffects.TryGetValue(appearancePointer, out effect))
            return;
        PersistentShadowEffects.Remove(appearancePointer);
        if (effect != null)
        {
            effect.SetActive(false);
            UnityEngine.Object.Destroy(effect);
        }
    }

    private static UnityEngine.Transform FindPresentationDescendant(
        UnityEngine.Transform root, string exactName)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, exactName, StringComparison.Ordinal))
            return root;
        for (int index = 0; index < root.childCount; index++)
        {
            UnityEngine.Transform found = FindPresentationDescendant(root.GetChild(index), exactName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static void EnsureSecondaryTracks(SD.CharacterAppearance appearance, bool wildHuntHorse)
    {
        if (appearance == null || appearance.Pointer == IntPtr.Zero ||
            appearance.sprenderer_charactermotion == null)
            return;
        UnityEngine.SpriteRenderer source = appearance.sprenderer_charactermotion;
        UnityEngine.Transform parent = source.transform;
        List<UnityEngine.GameObject> owned;
        if (!SecondaryTrackObjects.TryGetValue(appearance.Pointer, out owned) || owned == null)
        {
            owned = new List<UnityEngine.GameObject>();
            SecondaryTrackObjects[appearance.Pointer] = owned;
        }
        EnsureSecondaryRenderer(parent, source, owned, SecondaryTrailName, 0,
            UnityEngine.Vector3.zero, UnityEngine.Vector3.one, false);
        EnsureSecondaryRenderer(parent, source, owned, SecondaryTrail2Name, 0,
            UnityEngine.Vector3.zero, UnityEngine.Vector3.one, false);
        bool horseModeChanged;
        bool previousHorse;
        if (!SecondaryHorseWildHunt.TryGetValue(appearance.Pointer, out previousHorse))
            horseModeChanged = true;
        else
            horseModeChanged = previousHorse != wildHuntHorse;
        SecondaryHorseWildHunt[appearance.Pointer] = wildHuntHorse;
        UnityEngine.Vector3 horsePos = wildHuntHorse
            ? new UnityEngine.Vector3(-2.94f, 0f, 0f)
            : UnityEngine.Vector3.zero;
        UnityEngine.Vector3 horseScale = wildHuntHorse
            ? new UnityEngine.Vector3(-1.07f, 1.07f, 1.07f)
            : UnityEngine.Vector3.one;
        EnsureSecondaryRenderer(parent, source, owned, SecondaryHorseName, -1,
            horsePos, horseScale, horseModeChanged);
        EnsureSecondaryRenderer(parent, source, owned, SecondaryCoffinName, 1,
            new UnityEngine.Vector3(-9.872919f, 1.28f, 0f), UnityEngine.Vector3.one, false);
        EnsureSecondaryRenderer(parent, source, owned, SecondarySlashName, 2,
            new UnityEngine.Vector3(-0.45f, 4.68f, 0f), UnityEngine.Vector3.one, false);
        bool showIdle;
        if (IdleModes.TryGetValue(appearance.Pointer, out showIdle) && showIdle)
        {
            UnityEngine.SpriteRenderer idle;
            if (IdleRenderers.TryGetValue(appearance.Pointer, out idle) && idle != null)
                SetSecondaryOwnedVisible(owned, false);
        }
    }

    private static void SetSecondaryTracksVisible(SD.CharacterAppearance appearance, bool visible)
    {
        if (appearance == null || appearance.Pointer == IntPtr.Zero)
            return;
        List<UnityEngine.GameObject> owned;
        if (!SecondaryTrackObjects.TryGetValue(appearance.Pointer, out owned))
            return;
        SetSecondaryOwnedVisible(owned, visible);
    }

    private static void SetSecondaryOwnedVisible(List<UnityEngine.GameObject> owned, bool visible)
    {
        if (owned == null)
            return;
        for (int index = 0; index < owned.Count; index++)
        {
            if (owned[index] == null)
                continue;
            UnityEngine.SpriteRenderer renderer =
                owned[index].GetComponent<UnityEngine.SpriteRenderer>();
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    private static void EnsureSecondaryRenderer(
        UnityEngine.Transform parent,
        UnityEngine.SpriteRenderer source,
        List<UnityEngine.GameObject> owned,
        string name,
        int sortingOrder,
        UnityEngine.Vector3 localPosition,
        UnityEngine.Vector3 localScale,
        bool applyTransform)
    {
        UnityEngine.Transform existing = parent.Find(name);
        UnityEngine.GameObject go;
        UnityEngine.SpriteRenderer renderer;
        bool created = false;
        if (existing != null)
        {
            go = existing.gameObject;
            renderer = go.GetComponent<UnityEngine.SpriteRenderer>();
            if (renderer == null)
                renderer = go.AddComponent<UnityEngine.SpriteRenderer>();
            if (!owned.Contains(go))
                owned.Add(go);
        }
        else
        {
            go = new UnityEngine.GameObject(name);
            go.transform.SetParent(parent, false);
            renderer = go.AddComponent<UnityEngine.SpriteRenderer>();
            renderer.sprite = null;
            owned.Add(go);
            created = true;
        }
        if (created || applyTransform)
        {
            go.transform.localPosition = localPosition;
            go.transform.localRotation = UnityEngine.Quaternion.identity;
            go.transform.localScale = localScale;
        }
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = sortingOrder;
    }
 }

 internal static class CharacterAppearance_ChangeMotion_BlankDomainVisual_Patch
 {
    [HarmonyPriority(Priority.Last)]
    static bool Prefix(SD.CharacterAppearance __instance, ref MOTION_DETAIL __0, ref int __2)
    {
        bool run = BlankDomainVisualAccess.RemapMotion(__instance, ref __0);
        if (run && (int)__0 == 25 && BlankDomainVisualAccess.IsActive(__instance._battleUnitView))
        {
            var motion = __instance.GetMotion(__0, false);
            int count = motion != null && motion.timelineAssets != null ? motion.timelineAssets.Count : 0;
            // The donor's global parry has five variants; our private parry
            // has three. Keep a preselected native index inside that list.
            if (count > 0 && __2 >= count)
                __2 %= count;
        }
        return run;
    }

    [HarmonyPriority(Priority.Last)]
    static void Postfix(SD.CharacterAppearance __instance, MOTION_DETAIL __0)
    {
        BlankDomainVisualAccess.OnMotionChanged(__instance, __0);
    }
 }

 internal static class CharacterAppearance_Parrying_BlankDomainVisual_Patch
 {
    [HarmonyPriority(Priority.Last)]
    static bool Prefix(SD.CharacterAppearance __instance, ref MOTION_DETAIL __0)
    {
        return BlankDomainVisualAccess.RemapMotion(__instance, ref __0);
    }

    static void Postfix(SD.CharacterAppearance __instance, MOTION_DETAIL __0)
    {
        BlankDomainVisualAccess.OnMotionChanged(__instance, __0);
    }
 }

// Opt-in diagnostic: compare native lifetime with the old CoreCLR-only
// ownership against DontUnloadUnusedAsset, using the real Unity process.
// Normal installations do not create probes or request a resource sweep.
internal static class BlankDomainAssetLifetimeProbe
{
    private static UnityEngine.Texture2D controlTexture, protectedTexture;
    private static UnityEngine.Sprite controlSprite, protectedSprite;
    private static UnityEngine.AsyncOperation sweep;
    private static bool enabled, allowSweep, completed, sweepRequested;
    private static string lastScene;
    private static float sceneSince;
    private static string reportPath;

    internal static void Initialize()
    {
        string request = Path.Combine(Plugin.ResolvePluginRoot(), "_handover", "asset-lifetime-probe.once");
        if (!File.Exists(request)) return;
        allowSweep = File.ReadAllText(request).Trim() == "sweep-in-main";
        File.Delete(request);
        reportPath = Path.Combine(Plugin.ResolvePluginRoot(), "_handover", "asset-lifetime-probe.log");
        controlTexture = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
        protectedTexture = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
        protectedTexture.hideFlags = UnityEngine.HideFlags.DontUnloadUnusedAsset;
        controlTexture.Apply(false, false);
        protectedTexture.Apply(false, false);
        controlSprite = UnityEngine.Sprite.Create(controlTexture, new UnityEngine.Rect(0, 0, 2, 2), new UnityEngine.Vector2(.5f, .5f), 100f);
        protectedSprite = UnityEngine.Sprite.Create(protectedTexture, new UnityEngine.Rect(0, 0, 2, 2), new UnityEngine.Vector2(.5f, .5f), 100f);
        protectedSprite.hideFlags = UnityEngine.HideFlags.DontUnloadUnusedAsset;
        enabled = true;
        File.WriteAllText(reportPath, "Gsound " + Plugin.PluginVersion + " asset lifetime A/B " + DateTime.Now.ToString("O") + Environment.NewLine);
        Report("created; controlIds=" + controlSprite.GetInstanceID() + "/" + controlTexture.GetInstanceID() +
            "; protectedIds=" + protectedSprite.GetInstanceID() + "/" + protectedTexture.GetInstanceID());
    }

    private static void Report(string message)
    {
        string line = message + "; controlSprite=" + (controlSprite != null) +
            "; controlTexture=" + (controlTexture != null) + "; controlManagedNull=" + ReferenceEquals(controlSprite, null) +
            "; protectedSprite=" + (protectedSprite != null) + "; protectedTexture=" + (protectedTexture != null) +
            "; frames=" + BlankDomainVisualAccess.RuntimeArtStatus();
        Plugin.Logger?.LogInfo("Blank Domain asset lifetime probe: " + line);
        File.AppendAllText(reportPath, line + Environment.NewLine);
    }

    internal static void Tick()
    {
        if (!enabled || completed) return;
        try
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene != lastScene)
            {
                lastScene = scene;
                sceneSince = UnityEngine.Time.unscaledTime;
                Report("scene=" + scene);
            }
            if (controlSprite == null || controlTexture == null || (sweep != null && sweep.isDone))
            {
                bool pass = controlSprite == null && controlTexture == null && protectedSprite != null && protectedTexture != null;
                Report((pass ? "CONFIRMED_UNPROTECTED_ASSETS_COLLECTED" : "INCONCLUSIVE_OR_FAILED") +
                    "; explicitSweep=" + sweepRequested);
                completed = true;
                if (controlSprite != null) UnityEngine.Object.Destroy(controlSprite);
                if (controlTexture != null) UnityEngine.Object.Destroy(controlTexture);
                if (protectedSprite != null) UnityEngine.Object.Destroy(protectedSprite);
                if (protectedTexture != null) UnityEngine.Object.Destroy(protectedTexture);
                return;
            }
            // Natural scene unloading is preferred. An explicit sweep is allowed
            // only by the one-shot request and after the main menu is stable.
            if (allowSweep && !sweepRequested && (scene == "Main" || scene == "MainScene") &&
                UnityEngine.Time.unscaledTime - sceneSince >= 10f)
            {
                Report("requesting unused asset sweep in stable main menu");
                sweepRequested = true;
                sweep = UnityEngine.Resources.UnloadUnusedAssets();
            }
        }
        catch (Exception ex)
        {
            completed = true;
            Plugin.Logger?.LogError("Blank Domain asset lifetime probe failed: " + ex);
        }
    }
}

public sealed class BlankDomainPresentationDriver : UnityEngine.MonoBehaviour
{
    public BlankDomainPresentationDriver(IntPtr pointer) : base(pointer) { }
    private int ticks;
    public void LateUpdate()
    {
        try
        {
            ticks++;
            if (ticks == 1 || ticks == 120)
                Plugin.Logger?.LogInfo("Blank Domain persistent LateUpdate driver ticks=" + ticks);
            BlankDomainAssetLifetimeProbe.Tick();
            BlankDomainVisualAccess.LateUpdatePresentation();
        }
        catch (Exception ex)
        {
            if (ticks <= 120) Plugin.Logger?.LogWarning("Blank Domain driver failed: " + ex);
        }
    }
}

internal static class CharacterAppearance_DefaultSpine_BlankDomain_Patch
{
    static void Prefix(SD.CharacterAppearance __instance, ref bool __0, ref bool __1)
    {
        if (!BlankDomainVisualAccess.OwnsPresentation(__instance)) return;
        __0 = false;
        __1 = false;
        BlankDomainVisualAccess.SuppressDefaultSpine(__instance);
    }
    [HarmonyPriority(Priority.Last)]
    static void Postfix(SD.CharacterAppearance __instance)
    {
        BlankDomainVisualAccess.UpdatePresentation(__instance);
    }
}

internal static class CharacterAppearance_Update_BlankDomainVisual_Patch
{
    static void Postfix(SD.CharacterAppearance __instance)
    {
        BlankDomainVisualAccess.UpdatePresentation(__instance);
    }
}

 internal static class CharacterAppearance_BindTimeline_BlankDomain_Patch
 {
    [HarmonyPriority(Priority.Last)]
    static void Postfix(SD.CharacterAppearance __instance, UnityEngine.Timeline.TimelineAsset __0)
    {
        BlankDomainActionEffectBindingAccess.BindCurrentTimeline(__instance, __0);
    }
 }

 internal static class EffectActivateTimelineClip_BlankDomain_Patch
 {
    static void Prefix(EffectActivateTimelineClip __instance, UnityEngine.Playables.PlayableGraph __0)
    {
        BlankDomainActionEffectBindingAccess.BindPlayableReference(__instance, __0);
    }
 }

 internal static class BattleUnitModel_RecoverAllBreak_GsoundMotion_Patch
 {
    static void Prefix(BattleUnitModel __instance)
    {
        GsoundRecoverMotionAccess.Arm(__instance);
    }

    static void Postfix(BattleUnitModel __instance)
    {
        // Some combat paths never call BattleUnitView.SetUnRetreat. Playing
        // from the authoritative recovery method makes the custom recovery
        // animation deterministic; PlayIfArmed de-duplicates the view hook.
        if (__instance == null)
            return;
        BattleUnitView view = BattleObjectManager.Instance.GetView(__instance);
        GsoundRecoverMotionAccess.PlayIfArmed(view);
    }
 }

 internal static class BattleUnitView_SetUnRetreat_GsoundMotion_Patch
 {
    static void Postfix(BattleUnitView __instance)
    {
        GsoundRecoverMotionAccess.PlayIfArmed(__instance);
    }
 }

 internal static class GsoundHeatCoatPassiveModelDisplayPatch
 {
    static bool Prefix(PassiveModel __instance, ref bool __result)
    {
        if (__instance == null || __instance.GetID() != 1021602 ||
            !StageFieldAccess.IsGsoundUnit(__instance.Owner)) return true;
        __result = false;
        return false;
    }
 }

 internal static class GsoundHeatCoatPassiveBoxDisplayPatch
 {
    internal static void Prefix(ref Il2CppSystem.Collections.Generic.List<UnitInformationSkillListData.PassiveBoxData> __0)
    {
        if (__0 == null) return;
        // UI data is also used outside battle, where PassiveModel.Owner may be
        // null. The Gsound-only passive IDs identify that card set unambiguously.
        bool gsound = false, hasCoat = false;
        for (int i = 0; i < __0.Count; i++)
        {
            var passive = __0[i] != null ? __0[i].passiveData : null;
            if (passive == null) continue;
            int id = passive.GetID();
            if (id >= 10797001 && id <= 10797012) gsound = true;
            if (id == 1021602) hasCoat = true;
        }
        if (!gsound || !hasCoat) return;
        // PassiveBoxData is a native value type represented by a managed class.
        // This interop List<T>.Add passes its boxed header without unboxing.
        // Rebuilding the list corrupts the UI fields (and native GC references).
        // RemoveAt operates on the existing native UI array without reboxing.
        for (int i = __0.Count - 1; i >= 0; i--)
        {
            var box = __0[i];
            if (box != null && box.passiveData != null && box.passiveData.GetID() == 1021602)
                __0.RemoveAt(i);
        }
    }
 }

 internal static class GsoundHeatCoatPassiveDataDisplayPatch
 {
    static void Postfix(UnitInformationSkillListData __instance)
    {
        if (__instance == null) return;
        var boxes = __instance._passiveDataList;
        GsoundHeatCoatPassiveBoxDisplayPatch.Prefix(ref boxes);
        __instance._passiveDataList = boxes;
    }
 }

 internal static class GsoundPassiveUnlockPatch
 {
    internal static bool IsScript(string value)
    {
        return value != null && value.StartsWith("Modular/", StringComparison.Ordinal) &&
            value.IndexOf("/LUA:gsound/", StringComparison.Ordinal) >= 0;
    }

    internal static void Prefix(ref Il2CppSystem.Collections.Generic.List<string> __0)
    {
        if (__0 == null) return;
        bool found = false;
        for (int i = 0; i < __0.Count; i++) if (IsScript(__0[i])) { found = true; break; }
        if (!found) return;
        // String is a reference type; unlike PassiveBoxData it is safe to copy.
        // Keep real level/uptie requirements and the gameplay script list intact.
        var conditions = new Il2CppSystem.Collections.Generic.List<string>();
        for (int i = 0; i < __0.Count; i++) if (!IsScript(__0[i])) conditions.Add(__0[i]);
        __0 = conditions;
    }
 }

 internal static class GsoundCounterAccess
 {
    internal const int Counter = 1079724, CounterTwo = 1079725, CounterThree = 1079726;
    internal sealed class RoundState
    {
        internal bool GuardEquipped;
        internal int Resonance, Selected, DamageLogs;
        internal readonly HashSet<IntPtr> Allowed = new HashSet<IntPtr>();
        internal readonly HashSet<IntPtr> Used = new HashSet<IntPtr>();
    }
    private static readonly Dictionary<IntPtr, RoundState> States = new Dictionary<IntPtr, RoundState>();

    internal static bool IsCounter(int id) { return id >= Counter && id <= CounterThree; }
    internal static int Select(int total) { return total >= 3 ? CounterThree : total >= 2 ? CounterTwo : Counter; }
    internal static void Clear() { States.Clear(); }
    internal static void Reset(BattleUnitModel model) { if (model != null) States.Remove(model.Pointer); }

    internal static void Install(Harmony harmony)
    {
        Hook(harmony, typeof(SinManager), "OnCompleteCommand", "CommandsCommitted", false);
        Hook(harmony, typeof(SinManager), "OnRoundStart_Before", "RoundReset", true);
        Hook(harmony, typeof(BattleActionModelManager), "GetDefenseAction", "CheckDefense", false);
        Hook(harmony, typeof(BattleActionModel), "OnStartTurn_BeforeLog", "CounterStarted", true);
        Hook(harmony, typeof(BattleActionModel), "GetAttackDmgMultiplier", "IncomingDamage", false);
        Plugin.Logger?.LogInfo("Gsound counter hooks: confirmed commands, total VIOLET perfect resonance, leftmost 3, incoming one-sided damage.");
    }
    private static void Hook(Harmony h, Type type, string name, string callback, bool prefix)
    {
        var method = AccessTools.Method(type, name);
        if (method == null) throw new MissingMethodException(type.FullName, name);
        var patch = new HarmonyMethod(AccessTools.Method(typeof(GsoundCounterAccess), callback));
        patch.priority = Priority.Last;
        h.Patch(method, prefix ? patch : null, prefix ? null : patch);
    }
    internal static void RoundReset() { Clear(); }

    internal static void CommandsCommitted(SinManager __instance, BATTLE_EVENT_TIMING __0)
    {
        if (__0 != BATTLE_EVENT_TIMING.ON_BATTLE_START || __instance == null) return;
        try
        {
            var slots = __instance.GetActionListByFaction(UNIT_FACTION.PLAYER);
            if (slots == null) return;
            var models = new Dictionary<IntPtr, BattleUnitModel>();
            for (int i = 0; i < slots.Count; i++)
            {
                var model = slots[i]?.UnitModel;
                if (StageFieldAccess.IsGsoundUnit(model) && BlankDomainStateAccess.IsEntered(model))
                    models[model.Pointer] = model;
            }
            foreach (var model in models.Values)
            {
                // A repeated command-complete callback must not re-arm consumed counters.
                if (States.ContainsKey(model.Pointer)) continue;
                var state = new RoundState();
                var guards = new List<SinActionModel>();
                var unitSlots = model.GetSinActionList();
                if (unitSlots != null) for (int i = 0; i < unitSlots.Count; i++)
                {
                    var slot = unitSlots[i];
                    var action = slot?.CurrentBattleAction;
                    if (action != null && IsCounter(action.GetSkillID())) guards.Add(slot);
                }
                // Native GetFirstDefenseAction uses the same ascending slot index.
                guards.Sort((a, b) => a.GetSlotIndex().CompareTo(b.GetSlotIndex()));
                state.GuardEquipped = guards.Count > 0;
                state.Resonance = __instance.GetSumOfPerfectResonance(UNIT_FACTION.PLAYER, ATTRIBUTE_TYPE.VIOLET);
                state.Selected = Select(state.Resonance);
                States[model.Pointer] = state;
                for (int i = 0; i < guards.Count && i < 3; i++)
                {
                    var action = guards[i].CurrentBattleAction;
                    state.Allowed.Add(action.Pointer);
                    if (action.GetSkillID() != state.Selected && !action.TryChangeSkill(state.Selected))
                        Plugin.Logger?.LogWarning("Gsound counter skill change failed: slot=" + guards[i].GetSlotIndex() + ", skill=" + state.Selected);
                }
                Plugin.Logger?.LogInfo("Gsound counter snapshot: model=" + model.Pointer + "; totalEnvy=" + state.Resonance +
                    "; guard=" + state.GuardEquipped + "; skill=" + state.Selected + "; allowed=" + state.Allowed.Count);
            }
        }
        catch (Exception ex) { Plugin.Logger?.LogError("Gsound counter snapshot failed: " + ex); }
    }

    internal static bool Allowed(BattleActionModel action)
    {
        if (action == null || !StageFieldAccess.IsGsoundUnit(action.Model) ||
            !BlankDomainStateAccess.IsEntered(action.Model) || !IsCounter(action.GetSkillID())) return true;
        if (!States.TryGetValue(action.Model.Pointer, out var state)) return true;
        return state.Allowed.Contains(action.Pointer) && state.Used.Count < 3 && !state.Used.Contains(action.Pointer);
    }
    internal static void CheckDefense(ref bool __result, ref BattleActionModel __6)
    {
        if (__result && !Allowed(__6)) { __result = false; __6 = null; }
    }
    internal static void CounterStarted(BattleActionModel __instance)
    {
        if (__instance == null || !StageFieldAccess.IsGsoundUnit(__instance.Model) || !IsCounter(__instance.GetSkillID())) return;
        if (States.TryGetValue(__instance.Model.Pointer, out var state) && state.Allowed.Contains(__instance.Pointer) && state.Used.Add(__instance.Pointer))
            Plugin.Logger?.LogInfo("Gsound counter executed: skill=" + __instance.GetSkillID() + "; used=" + state.Used.Count + "/3");
    }
    internal static void IncomingDamage(BattleUnitModel __1, bool __4, ref float __result)
    {
        if (!__4 || !StageFieldAccess.IsGsoundUnit(__1) || !BlankDomainStateAccess.IsEntered(__1) ||
            !States.TryGetValue(__1.Pointer, out var state) || !state.GuardEquipped) return;
        // Native damage modifiers are additive ratios (Modular dmgmult uses *0.01).
        // Apply -50 percentage points only to this attack against this defender.
        // Do not store it in the defender's Modular atkMultAdder/outgoing passive.
        __result -= 0.5f;
        if (state.DamageLogs++ < 3)
            Plugin.Logger?.LogInfo("Gsound WanHun one-sided incoming damage modifier applied: -50%.");
    }
 }

 internal static class BlankDomainStateAccess
 {
    private static readonly HashSet<IntPtr> EnteredModels = new HashSet<IntPtr>();

    internal static void MarkEntered(BattleUnitModel model)
    {
        if (model != null && model.Pointer != IntPtr.Zero)
            EnteredModels.Add(model.Pointer);
    }

    internal static bool IsEntered(BattleUnitModel model)
    {
        return model != null && model.Pointer != IntPtr.Zero &&
            EnteredModels.Contains(model.Pointer);
    }

    internal static void Reset(BattleUnitModel model)
    {
        if (model != null && model.Pointer != IntPtr.Zero)
        {
            EnteredModels.Remove(model.Pointer);
            GsoundCounterAccess.Reset(model);
        }
    }

    internal static void Clear()
    {
        EnteredModels.Clear();
        GsoundCounterAccess.Clear();
    }
 }

 internal static class BlankDomainSlotAccess
 {
    private static readonly int[][] Replacements =
    {
        new[] { 1079701, 1079721 },
        new[] { 1079711, 1079721 },
        new[] { 1079712, 1079721 },
        new[] { 1079702, 1079722 },
        new[] { 1079703, 1079723 },
        new[] { 1079713, 1079723 },
        new[] { 1079714, 1079723 },
        new[] { 1079704, 1079724 },
        new[] { 1079705, 1079724 },
        new[] { 1079706, 1079724 }
    };

    internal static void Replace(BattleUnitModel model)
    {
        if (model == null || model.Pointer == IntPtr.Zero)
            return;
        Plugin.Logger?.LogInfo("Blank Domain dashboard replacement begin: model=" + model.Pointer);
        int mapped = 0;
        try
        {
            var actions = model.GetSinActionList();
            int actionCount = actions != null ? actions.Count : 0;
            for (int i = 0; i < actionCount; i++)
            {
                var action = actions[i];
                for (int index = 0; index < Replacements.Length; index++)
                {
                    int oldId = Replacements[index][0];
                    int newId = Replacements[index][1];
                    if (action != null)
                    {
                        try { action.ReplaceSkillAtoB(oldId, newId, true); } catch { }
                        try { action.ReplaceSkillOneAtoBForCurrentSinOnly(oldId, newId, true); } catch { }
                        try { action.ChangeReplacedSinByDefenseSkillAtoB(oldId, newId); } catch { }
                    }
                    try
                    {
                        if (model.ReplaceSkillAtoB(oldId, newId, i))
                            mapped++;
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Blank Domain Replace failed: " + ex.Message);
        }
        Plugin.Logger?.LogInfo("Blank Domain dashboard skills replaced via C# (" + mapped + " mappings succeeded).");
    }
 }

 internal sealed class BlankDomainSlotConsequence : IModularConsequence
 {
    public void ExecuteConsequence(ModularSA modular, string functionName, string rawParams, string[] args)
    {
        string selector = args != null && args.Length > 0 ? args[0] : "Self";
        var targets = modular.GetTargetModelList(selector);
        if (targets == null)
            return;
        var enumerator = targets.GetEnumerator();
        while (enumerator.MoveNext())
        {
            BattleUnitModel model = enumerator.Current;
            if (StageFieldAccess.IsGsoundUnit(model))
                BlankDomainSlotAccess.Replace(model);
        }
    }
 }

 internal sealed class BlankDomainStateAcquirer : IModularAcquirer
 {
    public int ExecuteAcquirer(ModularSA modular, string functionName, string rawParams, string[] args)
    {
        string selector = args != null && args.Length > 0 ? args[0] : "Self";
        var targets = modular.GetTargetModelList(selector);
        if (targets == null)
            return 0;
        var enumerator = targets.GetEnumerator();
        while (enumerator.MoveNext())
        {
            BattleUnitModel model = enumerator.Current;
            if (StageFieldAccess.IsGsoundUnit(model) && BlankDomainStateAccess.IsEntered(model))
                return 1;
        }
        return 0;
    }
 }

 internal sealed class GsoundMotionConsequence : IModularConsequence
 {
    public void ExecuteConsequence(ModularSA modular, string functionName, string rawParams, string[] args)
    {
        if (args == null || args.Length < 2)
            return;

        string operation = args[0]?.Trim().ToLowerInvariant();
        var targets = modular.GetTargetModelList(args[1]);
        if (targets == null)
            return;

        var enumerator = targets.GetEnumerator();
        while (enumerator.MoveNext())
        {
            BattleUnitModel model = enumerator.Current;
            if (!StageFieldAccess.IsGsoundUnit(model))
                continue;
            if (operation == "reset")
            {
                // Init belongs to a new encounter, whose model pointer may
                // differ from the previous one. Release the previous registry.
                BlankDomainVisualAccess.Clear();
                BlankDomainStateAccess.Clear();
                BlankDomainActionEffectBindingAccess.Clear();
                continue;
            }
            if (operation == "transform")
            {
                // Commit the bridge-side state before the 0.22s white flash.
                // AfterSlots can run during that interval and cannot reliably
                // access Modular's per-unit data table on this game build.
                BlankDomainStateAccess.MarkEntered(model);
            }
            BattleUnitView view = BattleObjectManager.Instance.GetView(model);
            if (view == null)
                continue;

            if (operation == "transform")
                GsoundRecoverMotionAccess.PlayTransformation(view);
            else if (operation == "recover")
                GsoundRecoverMotionAccess.PlayIfArmed(view);
        }
    }
 }

 internal static class ThumbActionEffectBindingAccess
 {
    private const string RuntimeRootName = "Gsound_Thumb_Action_Effect_Bindings";
    private static readonly Dictionary<IntPtr, UnityEngine.GameObject> Donors =
        new Dictionary<IntPtr, UnityEngine.GameObject>();

    // Exposed-reference keys used by the copied Thumb Disciple S3/S4
    // timelines. Rebinding these keys to a live donor instance lets Gsound's
    // own timeline activate the original particle/trail nodes at the exact
    // original timings, without running a second skill timeline.
    private static readonly int[] ExposedReferenceKeys =
    {
        -2135040548, -2133164185, -2118302047, -2080816060, -2074676799,
        -2045364382, -2014002455, -2002579060, -1988017929, -1978296260,
        -1911018227, -1770786991, -1755590569, -1702201284, -1695154989,
        -1688535920, -1673617366, -1626326850, -1582814099, -1544599625,
        -1536635601, -1490561262, -1464623645, -1410756938, -1409021198,
        -1401899243, -1400229948, -1394768059, -1349353575, -1348973103,
        -1299591806, -1261914420, -1250209447, -1204144206, -1196361077,
        -1154579318, -1144470871, -1125612167, -1114174332, -1093398300,
        -995518111, -985999708, -950978658, -948197627, -935146032,
        -894828094, -883571465, -844829587, -838291936, -816134338,
        -743078461, -727477902, -713630674, -710859476, -680579140,
        -635874687, -605467940, -601526365, -547131960, -532262056,
        -516161912, -485843027, -473172451, -408409367, -299402479,
        -280364986, -212102566, -212073441, -172253835, -150774280,
        -144804872, -138428708, -130147318, -100683278, -96132560,
        -91012513, 16143492, 29192713, 32092385, 39075000, 62563553,
        101215380, 161531421, 188188159, 192423743, 239428237,
        266848974, 293832766, 365889146, 467984079, 505396010,
        559847370, 591553308, 595345996, 621864846, 647177228,
        677819972, 689199288, 696776069, 751041684, 773138157,
        775781506, 793013007, 832321995, 840352100, 875777465,
        954299747, 998106738, 998212290, 1002944265, 1006441079,
        1083445539, 1085925859, 1087227149, 1096656762, 1133362831,
        1155000025, 1182655775, 1195602089, 1241350634, 1260197460,
        1260226968, 1261620442, 1300332801, 1325906223, 1354586437,
        1370690503, 1373791187, 1382742656, 1426793076, 1516689706,
        1597218133, 1608813143, 1622685841, 1649083350, 1788537510,
        1849164707, 1851502010, 1868540910, 1919788426, 1979157853,
        2005429456, 2046034270, 2081090903, 2101812475, 2120804331
    };

    private static readonly string[] EffectRootNames =
    {
        "BGBurn_Start", "Damage1", "Damage2", "Move1", "SwordLight1",
        "SwordLight1 (1)", "Trail_B", "Trail1", "Trail2"
    };

    internal static void Ensure(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero)
            return;
        BattleUnitModel model = view.unitModel;
        if (!StageFieldAccess.IsGsoundUnit(model) || view.Appearance == null ||
            BlankDomainStateAccess.IsEntered(model) || BlankDomainVisualAccess.IsActive(view))
            return;

        IntPtr key = view.Pointer;
        UnityEngine.GameObject donor;
        if (!Donors.TryGetValue(key, out donor) || donor == null)
        {
            UnityEngine.Transform parent = view.Appearance.transform.parent;
            if (parent == null)
                parent = view.transform;
            donor = Plugin.InstantiatePrivateThumbDonor(parent);
            if (donor == null)
            {
                Plugin.Logger?.LogWarning("Thumb action FX: private donor prefab could not be instantiated");
                return;
            }

            donor.name = RuntimeRootName;
            donor.SetActive(false);
            UnityEngine.Transform source = view.Appearance.transform;
            donor.transform.localPosition = source.localPosition;
            donor.transform.localRotation = source.localRotation;
            donor.transform.localScale = source.localScale;
            HideDonorCharacter(donor);
            Donors[key] = donor;
        }

        Bind(view, donor);
    }

    private static void Bind(BattleUnitView view, UnityEngine.GameObject donor)
    {
        try
        {
            UnityEngine.Playables.PlayableDirector mainDirector =
                view.Appearance.GetComponentInChildren<UnityEngine.Playables.PlayableDirector>(true);
            UnityEngine.Playables.PlayableDirector donorDirector =
                donor.GetComponentInChildren<UnityEngine.Playables.PlayableDirector>(true);
            if (mainDirector == null || donorDirector == null)
            {
                Plugin.Logger?.LogWarning("Thumb action FX: main or donor PlayableDirector is missing");
                return;
            }

            donorDirector.playOnAwake = false;
            donorDirector.Stop();
            donorDirector.enabled = false;
            DeactivateEffectRoots(donor.transform);
            donor.SetActive(true);
            // Some donor components restore their renderers from OnEnable.
            // Hide again after activation so only timeline-driven FX survive.
            HideDonorCharacter(donor);

            int rebound = 0;
            for (int index = 0; index < ExposedReferenceKeys.Length; index++)
            {
                // PropertyName.op_Implicit(int) in this IL2CPP interop build calls
                // a native int constructor that the game no longer exposes.
                // Assign the serialized id field directly instead.
                UnityEngine.PropertyName property = default(UnityEngine.PropertyName);
                property.id = ExposedReferenceKeys[index];
                bool valid;
                UnityEngine.Object target = donorDirector.GetReferenceValue(property, out valid);
                if (!valid || target == null)
                    continue;
                mainDirector.SetReferenceValue(property, target);
                rebound++;
            }

            Plugin.Logger?.LogInfo("Thumb action FX: rebound " + rebound +
                " live donor references for 过去/天台");
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Thumb action FX binding failed: " + ex);
        }
    }

    private static void HideDonorCharacter(UnityEngine.GameObject donor)
    {
        UnityEngine.Renderer[] renderers = donor.GetComponentsInChildren<UnityEngine.Renderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            UnityEngine.Renderer renderer = renderers[index];
            if (renderer == null)
                continue;
            if (renderer is UnityEngine.ParticleSystemRenderer)
                continue;
            if (renderer is UnityEngine.TrailRenderer)
                continue;
            if (renderer is UnityEngine.LineRenderer)
                continue;
            if (IsUnderEffectRoot(renderer.transform, donor.transform))
                continue;
            renderer.forceRenderingOff = true;
            renderer.enabled = false;
        }

        SD.CharacterAppearance appearance = donor.GetComponentInChildren<SD.CharacterAppearance>(true);
        if (appearance == null)
            return;

        if (appearance.sprenderer_charactermotion != null)
        {
            appearance.sprenderer_charactermotion.forceRenderingOff = true;
            appearance.sprenderer_charactermotion.enabled = false;
        }
        var parts = appearance.Sprenderer_charactermotion_Parts;
        if (parts != null)
        {
            for (int index = 0; index < parts.Count; index++)
            {
                if (parts[index] != null)
                {
                    parts[index].forceRenderingOff = true;
                    parts[index].enabled = false;
                }
            }
        }
        // Keep CharacterAppearance enabled: its callbacks drive the borrowed
        // Thumb timeline effects. Only the donor's visible body is hidden.
    }

    private static bool IsUnderEffectRoot(UnityEngine.Transform current, UnityEngine.Transform donorRoot)
    {
        while (current != null)
        {
            for (int index = 0; index < EffectRootNames.Length; index++)
            {
                if (string.Equals(current.name, EffectRootNames[index], StringComparison.Ordinal))
                    return true;
            }
            if (current == donorRoot)
                break;
            current = current.parent;
        }
        return false;
    }

    private static void DeactivateEffectRoots(UnityEngine.Transform root)
    {
        for (int index = 0; index < EffectRootNames.Length; index++)
        {
            UnityEngine.Transform found = FindDeep(root, EffectRootNames[index]);
            if (found != null && found.gameObject != null)
                found.gameObject.SetActive(false);
        }
    }

    private static UnityEngine.Transform FindDeep(UnityEngine.Transform root, string name)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, name, StringComparison.Ordinal))
            return root;
        for (int index = 0; index < root.childCount; index++)
        {
            UnityEngine.Transform match = FindDeep(root.GetChild(index), name);
            if (match != null)
                return match;
        }
        return null;
    }

    internal static void Clear()
    {
        foreach (UnityEngine.GameObject donor in Donors.Values)
        {
            if (donor != null)
                UnityEngine.Object.Destroy(donor);
        }
        Donors.Clear();
    }

    internal static void Remove(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero)
            return;
        UnityEngine.GameObject donor;
        if (!Donors.TryGetValue(view.Pointer, out donor))
            return;
        Donors.Remove(view.Pointer);
        if (donor != null)
        {
            donor.SetActive(false);
            UnityEngine.Object.Destroy(donor);
        }
    }
 }

 internal static class BlankDomainActionEffectBindingAccess
 {
    private sealed class Binding
    {
        internal string Property;
        internal string ObjectName;
        internal int ComponentIndex;
    }

    private sealed class Spec
    {
        internal string Key;
        internal string Bundle;
        internal string Prefab;
        internal Binding[] Bindings;
    }

    private sealed class RuntimeSpec
    {
        internal UnityEngine.GameObject Holder;
        internal readonly Dictionary<string, UnityEngine.Object> Targets =
            new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        internal IntPtr Appearance;
        internal IntPtr BoundDirector;
        internal UnityEngine.Playables.PlayableDirector Director;
        internal readonly HashSet<string> ResolvedProperties = new HashSet<string>();
        internal readonly Dictionary<IntPtr, EffectActivateTimelineClip> BoundClips =
            new Dictionary<IntPtr, EffectActivateTimelineClip>();
    }

    // Generated from all selected Timeline tracks, including GroupTrack children.
    private static readonly Spec[] Specs =
    {
        new Spec
        {
            Key = "skill1",
            Bundle = "GsoundBlankSkill1.bundle",
            Prefab = "Assets/Resources_moved/Prefab/SD/Enemy/8172_MaouHeathclif_MainAppearance.prefab",
            Bindings = new[]
            {
                Bind("GSBD_skill1_-1349527779", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Move1", 1),
                Bind("GSBD_skill1_-834979258", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Wind_P", 1),
                Bind("GSBD_skill1_-874548714", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Wind_P", 1),
                Bind("GSBD_skill1_1140555427", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Piercing1", 1),
                Bind("GSBD_skill1_442696225", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Trail1", 1),
            }
        },
        new Spec
        {
            Key = "skill2",
            Bundle = "GsoundBlankSkill2.bundle",
            Prefab = "Assets/Resources_moved/Prefab/SD/Enemy/8172_MaouHeathclif_MainAppearance.prefab",
            Bindings = new[]
            {
                Bind("GSBD_skill2_-1349527779", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Move1", 1),
                Bind("GSBD_skill2_-1556886749", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Piercing1", 1),
                Bind("GSBD_skill2_-652722925", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Move1", 1),
                Bind("GSBD_skill2_-82598358", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_HorseCharge", 1),
                Bind("GSBD_skill2_-834979258", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Wind_P", 1),
                Bind("GSBD_skill2_-874548714", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Wind_P", 1),
                Bind("GSBD_skill2_1140555427", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Piercing1", 1),
                Bind("GSBD_skill2_426621632", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Slash1", 2),
                Bind("GSBD_skill2_442696225", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Trail1", 1),
                Bind("GSBD_skill2_993750337", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Wind_P", 1),
            }
        },
        new Spec
        {
            Key = "skill3",
            Bundle = "GsoundBlankSkill3.bundle",
            Prefab = "Assets/Resources_moved/Prefab/SD/Personality/10710_Heathclif_WHuntAppearance.prefab",
            Bindings = new[]
            {
                Bind("GSBD_skill3_-1281764862", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_PC_6_Heathcliff_WildHunt_GhostRide", 1),
                Bind("GSBD_skill3_-1783187975", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_PC_6_Heathcliff_WildHunt_Piercing1", 3),
                Bind("GSBD_skill3_-375308940", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/ShotMove (1)", 3),
                Bind("GSBD_skill3_-946267271", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/GhostRideAddParticleback", 3),
                Bind("GSBD_skill3_1054463146", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_PC_6_Heathcliff_WildHunt_Wind1", 3),
                Bind("GSBD_skill3_1141148090", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/FX_PC_6_Heathcliff_WildHunt_Wind1 (1)", 3),
                Bind("GSBD_skill3_1335966292", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/Trail2 (1)", 3),
                Bind("GSBD_skill3_1720672717", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_PC_6_Heathcliff_WildHunt_GhostRide (1)", 1),
                Bind("GSBD_skill3_1743539179", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_PC_6_Heathcliff_WildHunt_Wind1", 3),
                Bind("GSBD_skill3_1792732585", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Trail2", 3),
                Bind("GSBD_skill3_1805491214", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/FX_PC_6_Heathcliff_WildHunt_Move1 (1)", 3),
                Bind("GSBD_skill3_1936407345", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/FX_PC_6_Heathcliff_WildHunt_Piercing1 (1)", 3),
                Bind("GSBD_skill3_2120571225", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/FX_PC_6_Heathcliff_WildHunt_Wind1 (1)", 3),
                Bind("GSBD_skill3_244490143", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_PC_6_Heathcliff_WildHunt_Move1", 3),
                Bind("GSBD_skill3_339656681", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/ShotMove", 3),
                Bind("GSBD_skill3_458215395", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Trail1", 3),
                Bind("GSBD_skill3_576893183", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/GhostRideAddParticle", 3),
                Bind("GSBD_skill3_904010276", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/Trail1 (1)", 3),
            }
        },
        new Spec
        {
            Key = "counter",
            Bundle = "GsoundBlankCounter.bundle",
            Prefab = "Assets/Resources_moved/Prefab/SD/Enemy/8172_MaouHeathclif_MainAppearance.prefab",
            Bindings = new[]
            {
                Bind("GSBD_counter_-1349527779", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Move1", 1),
                Bind("GSBD_counter_-1556886749", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Piercing1", 1),
                Bind("GSBD_counter_-652722925", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Move1", 1),
                Bind("GSBD_counter_-82598358", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_HorseCharge", 1),
                Bind("GSBD_counter_-834979258", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Wind_P", 1),
                Bind("GSBD_counter_-874548714", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Wind_P", 1),
                Bind("GSBD_counter_1140555427", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Piercing1", 1),
                Bind("GSBD_counter_426621632", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Slash1", 2),
                Bind("GSBD_counter_442696225", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_Mon_Cp6_DemonKing_Trail1", 1),
                Bind("GSBD_counter_993750337", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Wind_P", 1),
            }
        },
        new Spec
        {
            Key = "parry",
            Bundle = "GsoundBlankParry.bundle",
            Prefab = "Assets/Resources_moved/Prefab/SD/Personality/10710_Heathclif_WHuntAppearance.prefab",
            Bindings = new[]
            {
                Bind("GSBD_parry_-1062192605", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_PC_6_Heathcliff_WildHunt_Wind1", 3),
                Bind("GSBD_parry_-1521631466", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/Trail2 (1)", 3),
                Bind("GSBD_parry_-1970545176", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_PC_6_Heathcliff_WildHunt_Piercing1", 3),
                Bind("GSBD_parry_-297561770", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/FX_PC_6_Heathcliff_WildHunt_Wind1 (1)", 3),
                Bind("GSBD_parry_-753462969", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Trail1", 3),
                Bind("GSBD_parry_-833647779", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/Trail1 (1)", 3),
                Bind("GSBD_parry_-868842483", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Trail2", 3),
                Bind("GSBD_parry_2118728572", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_PC_6_Heathcliff_WildHunt_Move1", 3),
                Bind("GSBD_parry_469338741", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/FX_PC_6_Heathcliff_WildHunt_Wind1", 3),
                Bind("GSBD_parry_519393268", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/FX_PC_6_Heathcliff_WildHunt_Piercing1 (1)", 3),
                Bind("GSBD_parry_627003616", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/FX_PC_6_Heathcliff_WildHunt_Move1 (1)", 3),
                Bind("GSBD_parry_741937559", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/CoinBack/FX_PC_6_Heathcliff_WildHunt_Wind1 (1)", 3),
            }
        }
    };

    private static readonly Spec TransformSpec =
    new Spec
    {
        Key = "transform",
        Bundle = "GsoundBlankTransform.bundle",
        Prefab = "Assets/Resources_moved/Prefab/SD/Personality/10710_Heathclif_WHuntAppearance.prefab",
        Bindings = new[]
        {
            Bind("GSBD_transform_-406167833", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Start_Wind", 3),
            Bind("GSBD_transform_1687825524", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Start_HorseMove", 3),
            Bind("GSBD_transform_594186398", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Start_Smoke1", 3),
            Bind("GSBD_transform_849702529", "[Transform,Fixed]ScaleAndPositionPivot/[Transform]PivotForAnim/[Transform]DefaultEffectPivot/Start_Trail", 3),
        }
    };

    private static readonly Dictionary<IntPtr, Dictionary<string, RuntimeSpec>> Runtime =
        new Dictionary<IntPtr, Dictionary<string, RuntimeSpec>>();

    private static Binding Bind(string property, string objectName, int componentIndex)
    {
        return new Binding
        {
            Property = property,
            ObjectName = objectName,
            ComponentIndex = componentIndex
        };
    }

    internal static void Ensure(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero || view.Appearance == null ||
            !StageFieldAccess.IsGsoundUnit(view.unitModel) ||
            !BlankDomainVisualAccess.IsActive(view))
            return;

        Dictionary<string, RuntimeSpec> perView;
        if (!Runtime.TryGetValue(view.Pointer, out perView))
        {
            perView = new Dictionary<string, RuntimeSpec>(StringComparer.OrdinalIgnoreCase);
            Runtime[view.Pointer] = perView;
        }

        for (int index = 0; index < Specs.Length; index++)
            EnsureSpec(view, perView, Specs[index]);
    }

    internal static void EnsureTransformation(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero || view.Appearance == null ||
            !StageFieldAccess.IsGsoundUnit(view.unitModel))
            return;
        Dictionary<string, RuntimeSpec> perView;
        if (!Runtime.TryGetValue(view.Pointer, out perView))
        {
            perView = new Dictionary<string, RuntimeSpec>(StringComparer.OrdinalIgnoreCase);
            Runtime[view.Pointer] = perView;
        }
        EnsureSpec(view, perView, TransformSpec);
    }

    internal static bool IsPrivateTimeline(
        SD.CharacterAppearance appearance, UnityEngine.Timeline.TimelineAsset timeline)
    {
        if (appearance == null || timeline == null)
            return false;
        foreach (int detail in new[] { 14, 15, 16, 21, 24, 25 })
        {
            var motion = appearance.GetMotion((MOTION_DETAIL)detail, false);
            var assets = motion != null ? motion.timelineAssets : null;
            if (assets == null)
                continue;
            for (int i = 0; i < assets.Count; i++)
                if (assets[i] != null && assets[i].Pointer == timeline.Pointer)
                    return true;
        }
        return false;
    }

    private static readonly HashSet<string> GraphDiagnostics = new HashSet<string>();

    internal static void PrepareCurrentGraph(SD.CharacterAppearance appearance)
    {
        if (appearance == null || (!BlankDomainVisualAccess.IsTransforming(appearance) &&
            !BlankDomainVisualAccess.IsActive(appearance._battleUnitView)))
            return;
        var director = appearance._playableDirector;
        var timeline = director != null && director.playableAsset != null
            ? director.playableAsset.TryCast<UnityEngine.Timeline.TimelineAsset>() : null;
        if (!IsPrivateTimeline(appearance, timeline))
            return;
        try
        {
            // The native call already rebuilds. Rebuild again only if this
            // postcondition check had to repair references after that build.
            bool repaired = BindCurrentTimeline(appearance, timeline);
            if (repaired)
            {
                double time = director.time;
                director.RebuildGraph();
                director.time = time;
            }
            director.RebindPlayableGraphOutputs();
            // GetOutputCount's generated wrapper throws InvalidProgramException
            // in the installed interop. Track bindings above plus the native
            // RebindPlayableGraphOutputs API avoid that unsupported wrapper.
            string key = appearance.Pointer + ":" + timeline.Pointer;
            if (GraphDiagnostics.Add(key))
                Plugin.Logger?.LogInfo("Blank Domain graph checked: " + timeline.name +
                    "; repaired=" + repaired + ".");
        }
        catch (Exception ex)
        {
            if (GraphDiagnostics.Add("error:" + appearance.Pointer))
                Plugin.Logger?.LogWarning("Blank Domain graph repair failed: " + ex);
        }
    }

    internal static bool BindCurrentTimeline(
        SD.CharacterAppearance appearance, UnityEngine.Timeline.TimelineAsset timeline)
    {
        if (appearance == null || timeline == null)
            return false;
        BattleUnitView view = appearance._battleUnitView;
        bool transforming = BlankDomainVisualAccess.IsTransforming(appearance);
        if (!transforming && !BlankDomainVisualAccess.IsActive(view))
            return false;
        // This is the director the engine actually plays, after its native
        // BindTimelineTracks routine. A hierarchy search can select another
        // component, and pre-ChangeMotion references can be overwritten here.
        var director = appearance._playableDirector;
        if (director == null)
            return false;
        if (!IsPrivateTimeline(appearance, timeline))
            return false;
        if (transforming)
            EnsureTransformation(view);
        else
            Ensure(view);

        var tracks = timeline.flattenedTracks;
        if (tracks == null)
            return false;
        bool changed = false;
        int fxClips = 0;
        int verifiedClips = 0;
        foreach (var track in tracks)
        {
            if (track == null)
                continue;
            UnityEngine.Object binding = null;
            if (track.TryCast<UnityEngine.Timeline.AnimationTrack>() != null)
                binding = appearance._anim;
            else if (track.TryCast<EffectActivateTimelineTrack>() != null)
                binding = appearance;
            if (binding != null && director.GetGenericBinding(track) != binding)
            {
                director.SetGenericBinding(track, binding);
                changed = true;
            }
            foreach (var timelineClip in track.clips)
            {
                var clip = timelineClip.asset != null
                    ? timelineClip.asset.TryCast<EffectActivateTimelineClip>() : null;
                if (clip != null)
                {
                    fxClips++;
                    bool verified;
                    changed |= BindClipReference(clip, director, out verified);
                    if (verified)
                        verifiedClips++;
                }
            }
        }
        if (GraphDiagnostics.Add("refs:" + appearance.Pointer + ":" + timeline.Pointer))
            Plugin.Logger?.LogInfo("Blank Domain FX clips checked: " + timeline.name +
                "; live=" + verifiedClips + "/" + fxClips + ".");
        return changed;
    }

    private static bool BindClipReference(
        EffectActivateTimelineClip clip, UnityEngine.Playables.PlayableDirector director, out bool verified)
    {
        verified = false;
        foreach (var perView in Runtime.Values)
        foreach (var state in perView.Values)
        {
            if (state == null || state.BoundDirector != director.Pointer)
                continue;
            foreach (var pair in state.Targets)
            {
                var reference = clip.exposedReference;
                var property = new UnityEngine.PropertyName(pair.Key);
                // The asset tag is a stable identity independent of Unity's
                // serialized PropertyName conversion. Construct both sides at
                // runtime before native CreatePlayable resolves the reference.
                bool tagged = string.Equals(clip.name, "GSBD_REF:" + pair.Key, StringComparison.Ordinal);
                if ((!tagged && property.id != reference.exposedName.id) || pair.Value == null)
                    continue;
                bool valid = false;
                var resolved = director.GetReferenceValue(property, out valid);
                bool changed = property.id != reference.exposedName.id ||
                    !valid || resolved != pair.Value || reference.defaultValue != pair.Value;
                director.SetReferenceValue(property, pair.Value);
                reference.exposedName = property;
                reference.defaultValue = pair.Value;
                clip.exposedReference = reference;
                state.BoundClips[clip.Pointer] = clip;
                var check = clip.exposedReference.Resolve(director.Cast<UnityEngine.IExposedPropertyTable>());
                verified = check != null && check.Pointer == pair.Value.Pointer;
                if (state.ResolvedProperties.Add(pair.Key))
                    Plugin.Logger?.LogInfo("Blank Domain live FX verified: " + pair.Key +
                        "; target=" + pair.Value.name + "; resolved=" + verified + ".");
                return changed;
            }
        }
        return false;
    }

    internal static void BindPlayableReference(
        EffectActivateTimelineClip clip, UnityEngine.Playables.PlayableGraph graph)
    {
        if (clip == null || !clip.name.StartsWith("GSBD_REF:", StringComparison.Ordinal))
            return;
        // ExposedReference is resolved when the playable is created, not on
        // every frame. Write the live instance at this exact boundary, before
        // native CreatePlayable copies it into EffectActivateTimelineBehaviour.
        try
        {
            var resolver = graph.GetResolver();
            var director = resolver != null ? resolver.TryCast<UnityEngine.Playables.PlayableDirector>() : null;
            if (director == null) return;
            bool verified;
            BindClipReference(clip, director, out verified);
        }
        catch (Exception ex)
        {
            if (GraphDiagnostics.Add("playable:" + clip.Pointer))
                Plugin.Logger?.LogWarning("Blank Domain playable reference preparation failed: " + ex);
        }
    }

    private static void EnsureSpec(
        BattleUnitView view,
        Dictionary<string, RuntimeSpec> perView,
        Spec spec)
    {
        RuntimeSpec state;
        if (!perView.TryGetValue(spec.Key, out state) || state == null ||
            state.Holder == null || state.Appearance != view.Appearance.Pointer)
        {
            ReleaseSpec(state);
            state = Extract(view, spec);
            if (state == null)
                return;
            perView[spec.Key] = state;
        }
        // ChangeMotion keeps the same PlayableDirector component but rebuilds
        // its PlayableGraph. Always rewrite references after a motion change;
        // caching only the component pointer made extracted FX silently vanish.
        BindToDirector(view, state, spec);
    }

    internal static void SlowTransformationEffects(BattleUnitView view, float rate, float sourceDelta = 0f)
    {
        if (view == null || !Runtime.TryGetValue(view.Pointer, out var perView) ||
            !perView.TryGetValue(TransformSpec.Key, out var state) || state.Holder == null) return;
        // This holder is private and destroyed at cinematic end. Its Activation
        // tracks own lifetime; prevent attack-effect timers from ending it early.
        foreach (var effect in state.Holder.GetComponentsInChildren<CharacterAttackEffect>(true))
            if (effect != null) { effect._isNotDisable = true; effect.SetTimeScale(rate); }
        foreach (var animator in state.Holder.GetComponentsInChildren<UnityEngine.Animator>(true))
            if (animator != null) { animator.updateMode = UnityEngine.AnimatorUpdateMode.UnscaledTime; animator.speed = rate; }
        foreach (var particle in state.Holder.GetComponentsInChildren<UnityEngine.ParticleSystem>(true))
            if (particle != null)
            {
                var main = particle.main;
                main.simulationSpeed = 1f;
                particle.Pause(false);
                if (sourceDelta > 0f && particle.gameObject.activeInHierarchy)
                    particle.Simulate(sourceDelta, false, false, false);
            }
    }

    internal static void RemoveTransformation(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero)
            return;
        Dictionary<string, RuntimeSpec> perView;
        if (!Runtime.TryGetValue(view.Pointer, out perView) || perView == null)
            return;
        RuntimeSpec state;
        if (perView.TryGetValue(TransformSpec.Key, out state))
        {
            perView.Remove(TransformSpec.Key);
            ReleaseSpec(state);
        }
        if (perView.Count == 0)
            Runtime.Remove(view.Pointer);
    }

    private static RuntimeSpec Extract(BattleUnitView view, Spec spec)
    {
        UnityEngine.GameObject donor = null;
        UnityEngine.GameObject holder = null;
        try
        {
            UnityEngine.Transform donorParent = view.Appearance.transform.parent != null
                ? view.Appearance.transform.parent
                : view.transform;
            donor = Plugin.InstantiateBlankDomainDonor(spec.Bundle, spec.Prefab, donorParent);
            if (donor == null)
            {
                Plugin.Logger?.LogWarning("Blank Domain FX source missing: " + spec.Key);
                return null;
            }

            // Never leave a complete official CharacterAppearance active for
            // a render frame.  It is only a temporary object graph from which
            // the timeline-controlled effect components are detached.
            donor.name = "Gsound_BlankDomain_TemporarySource_" + spec.Key;
            donor.SetActive(false);
            donor.transform.localPosition = view.Appearance.transform.localPosition;
            donor.transform.localRotation = view.Appearance.transform.localRotation;
            donor.transform.localScale = view.Appearance.transform.localScale;

            UnityEngine.Transform mainPivot =
                FindDeep(view.Appearance.transform, "[Transform]DefaultEffectPivot");
            if (mainPivot == null)
                mainPivot = view.Appearance.transform;
            holder = new UnityEngine.GameObject(
                "Gsound_BlankDomain_StrippedFX_" + spec.Key);
            holder.transform.SetParent(mainPivot, false);
            holder.transform.localPosition = UnityEngine.Vector3.zero;
            holder.transform.localRotation = UnityEngine.Quaternion.identity;
            holder.transform.localScale = UnityEngine.Vector3.one;
            holder.SetActive(false);

            RuntimeSpec state = new RuntimeSpec
            {
                Holder = holder,
                Appearance = view.Appearance.Pointer
            };
            UnityEngine.Transform donorPivot =
                FindDeep(donor.transform, "[Transform]DefaultEffectPivot");
            if (donorPivot == null)
                donorPivot = donor.transform;

            // Resolve every full path before detaching anything. In particular,
            // CoinBack contains several separately referenced child effects.
            var roots = new Dictionary<IntPtr, UnityEngine.Transform>();
            int extractedCount = 0;
            for (int index = 0; index < spec.Bindings.Length; index++)
            {
                Binding binding = spec.Bindings[index];
                UnityEngine.Transform effect = donor.transform.Find(binding.ObjectName);
                if (effect == null)
                {
                    Plugin.Logger?.LogWarning("Blank Domain FX node missing for " +
                        spec.Key + ": " + binding.ObjectName);
                    continue;
                }

                UnityEngine.Component[] components =
                    effect.gameObject.GetComponents<UnityEngine.Component>();
                if (components == null || binding.ComponentIndex < 0 ||
                    binding.ComponentIndex >= components.Length ||
                    components[binding.ComponentIndex] == null)
                {
                    Plugin.Logger?.LogWarning("Blank Domain FX component missing for " +
                        spec.Key + ": " + binding.ObjectName + "[" +
                        binding.ComponentIndex + "]");
                    continue;
                }
                CharacterAttackEffect attackEffect =
                    components[binding.ComponentIndex].TryCast<CharacterAttackEffect>();
                if (attackEffect == null)
                {
                    Plugin.Logger?.LogWarning("Blank Domain FX target is not CharacterAttackEffect: " +
                        spec.Key + "/" + binding.ObjectName);
                    continue;
                }
                UnityEngine.Transform root = effect;
                while (root.parent != null && root.parent.Pointer != donorPivot.Pointer)
                    root = root.parent;
                if (root.parent == null)
                    throw new InvalidOperationException("FX target outside DefaultEffectPivot: " + binding.ObjectName);
                roots[root.Pointer] = root;
                effect.gameObject.SetActive(false);
                state.Targets[binding.Property] = attackEffect;
                extractedCount++;
            }

            foreach (var root in roots.Values)
            {
                // Move only the required effect subtree, preserving nested
                // transforms and references such as CoinBack/Trail1 (1).
                root.SetParent(holder.transform, false);
                var effects = root.gameObject.GetComponentsInChildren<CharacterAttackEffect>(true);
                foreach (var effect in effects)
                    if (effect != null)
                        effect.InitAppearance(view.Appearance);
                if (spec.Key == "skill1" || spec.Key == "skill2")
                {
                    int recolored = ApplyPurplePalette(root.gameObject);
                    if (recolored > 0)
                        Plugin.Logger?.LogInfo("Blank Domain FX " + spec.Key +
                            ": recolored " + recolored + " red particle/material channels to violet");
                }
            }

            // The holder must be active so Timeline Activation tracks can
            // toggle its still-inactive child effect objects.
            holder.SetActive(true);
            donor.SetActive(false);
            UnityEngine.Object.Destroy(donor);
            donor = null;
            Plugin.Logger?.LogInfo("Blank Domain FX " + spec.Key + ": extracted " +
                extractedCount + "/" + spec.Bindings.Length +
                " references; official character source destroyed");
            return state;
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Blank Domain FX extraction failed for " + spec.Key + ": " + ex);
            if (holder != null)
                UnityEngine.Object.Destroy(holder);
            if (donor != null)
            {
                donor.SetActive(false);
                UnityEngine.Object.Destroy(donor);
            }
            return null;
        }
    }

    private static UnityEngine.Color Purpleize(UnityEngine.Color color)
    {
        if (color.r <= color.b * 1.10f && color.r <= color.g * 1.10f)
            return color;
        float value = Math.Max(color.r, Math.Max(color.g, color.b));
        return new UnityEngine.Color(value * 0.60f, value * 0.08f, value, color.a);
    }

    private static int ApplyPurplePalette(UnityEngine.GameObject root)
    {
        if (root == null)
            return 0;
        int changed = 0;
        try
        {
            var particles = root.GetComponentsInChildren<UnityEngine.ParticleSystem>(true);
            if (particles != null)
                for (int i = 0; i < particles.Length; i++)
                {
                    var ps = particles[i];
                    if (ps == null) continue;
                    var before = ps.startColor;
                    var after = Purpleize(before);
                    if (after.r != before.r || after.g != before.g || after.b != before.b)
                    {
                        ps.startColor = after;
                        changed++;
                    }
                }
            var renderers = root.GetComponentsInChildren<UnityEngine.Renderer>(true);
            if (renderers != null)
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null) continue;
                    var material = renderer.material;
                    if (material == null || !material.HasColor("_Color")) continue;
                    var before = material.color;
                    var after = Purpleize(before);
                    if (after.r != before.r || after.g != before.g || after.b != before.b)
                    {
                        material.color = after;
                        changed++;
                    }
                }
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Blank Domain purple FX palette failed: " + ex.Message);
        }
        return changed;
    }

    private static void BindToDirector(BattleUnitView view, RuntimeSpec state, Spec spec)
    {
        try
        {
            UnityEngine.Playables.PlayableDirector main =
                view.Appearance._playableDirector;
            if (main == null || main.Pointer == IntPtr.Zero)
                return;

            int rebound = 0;
            for (int index = 0; index < spec.Bindings.Length; index++)
            {
                Binding binding = spec.Bindings[index];
                UnityEngine.Object target;
                if (!state.Targets.TryGetValue(binding.Property, out target) || target == null)
                    continue;
                UnityEngine.PropertyName property = new UnityEngine.PropertyName(binding.Property);
                main.SetReferenceValue(property, target);
                rebound++;
            }
            if (state.BoundDirector != main.Pointer)
                Plugin.Logger?.LogInfo("Blank Domain FX " + spec.Key + ": bound " + rebound +
                    "/" + spec.Bindings.Length + " live CharacterAttackEffect references to active director");
            state.BoundDirector = main.Pointer;
            state.Director = main;
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Blank Domain FX binding failed for " + spec.Key + ": " + ex);
        }
    }

    private static UnityEngine.Transform FindDeep(UnityEngine.Transform root, string name)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, name, StringComparison.Ordinal))
            return root;
        for (int index = 0; index < root.childCount; index++)
        {
            UnityEngine.Transform found = FindDeep(root.GetChild(index), name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static void ReleaseSpec(RuntimeSpec state)
    {
        if (state == null)
            return;
        foreach (var clip in state.BoundClips.Values)
        {
            if (clip == null)
                continue;
            var reference = clip.exposedReference;
            foreach (var target in state.Targets.Values)
            {
                if (reference.defaultValue == target)
                {
                    reference.defaultValue = null;
                    clip.exposedReference = reference;
                    break;
                }
            }
        }
        if (state.Director != null)
        {
            foreach (var pair in state.Targets)
            {
                bool valid;
                var key = new UnityEngine.PropertyName(pair.Key);
                if (state.Director.GetReferenceValue(key, out valid) == pair.Value)
                    state.Director.ClearReferenceValue(key);
            }
        }
        state.BoundClips.Clear();
        state.Targets.Clear();
        if (state.Holder != null)
        {
            state.Holder.SetActive(false);
            UnityEngine.Object.Destroy(state.Holder);
        }
    }

    internal static void Remove(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero)
            return;
        Dictionary<string, RuntimeSpec> perView;
        if (!Runtime.TryGetValue(view.Pointer, out perView))
            return;
        Runtime.Remove(view.Pointer);
        foreach (RuntimeSpec state in perView.Values)
            ReleaseSpec(state);
    }

    internal static void Clear()
    {
        foreach (Dictionary<string, RuntimeSpec> perView in Runtime.Values)
        {
            foreach (RuntimeSpec state in perView.Values)
                ReleaseSpec(state);
        }
        Runtime.Clear();
        GraphDiagnostics.Clear();
    }
 }

 internal static class GsoundVoiceMuteAccess
 {
    private static bool _logged;

    internal static void Patch(Harmony harmony)
    {
        if (harmony == null)
            return;

        int patched = 0;
        try
        {
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(SoundManager)))
            {
                if (method == null || method.IsAbstract)
                    continue;
                if (method.Name != "CreateInstance" &&
                    method.Name != "GetMultiVoiceInstance" &&
                    method.Name != "PlayOneShot" &&
                    method.Name != "PlayOneShotSFX" &&
                    method.Name != "PlayOneShotSFXToFullPath")
                    continue;
                if (method.ReturnType != typeof(EventInstance))
                    continue;
                var parameters = method.GetParameters();
                if (parameters.Length < 1 || parameters[0].ParameterType != typeof(string))
                    continue;
                harmony.Patch(method, new HarmonyMethod(typeof(GsoundVoiceMuteAccess), "Prefix"));
                patched++;
            }
            try
            {
                foreach (var method in AccessTools.GetDeclaredMethods(typeof(BattleUnitView)))
                {
                    if (method == null)
                        continue;
                    if (method.Name == "SetPlayVoice")
                    {
                        harmony.Patch(method, new HarmonyMethod(typeof(GsoundVoiceMuteAccess), "Prefix_SetPlayVoice"));
                        patched++;
                    }
                    else if (method.Name == "SetPlayVoice_Instance")
                    {
                        harmony.Patch(method, new HarmonyMethod(typeof(GsoundVoiceMuteAccess), "Prefix_SetPlayVoice_Instance"));
                        patched++;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning("BattleUnitView voice patch failed: " + ex.Message);
            }
            Plugin.Logger?.LogInfo("Gsound donor-voice mute patched " + patched + " voice/sound methods.");
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Gsound donor-voice mute patch failed: " + ex.Message);
        }
    }

    internal static void MuteAppearance(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero || view.Appearance == null)
            return;
        if (!StageFieldAccess.IsGsoundUnit(view.unitModel))
            return;

        try
        {
            view.SetMuteBattleVoice(true);
            view.SetMuteEGOVoice(true);
            view.SetMuteOrChangeBattleVoice_Produce(true, string.Empty);
        }
        catch { }

        try
        {
            FMODEventPlayable[] playables =
                view.Appearance.GetComponentsInChildren<FMODEventPlayable>(true);
            if (playables == null)
                return;
            int muted = 0;
            for (int i = 0; i < playables.Length; i++)
            {
                FMODEventPlayable playable = playables[i];
                if (playable == null)
                    continue;
                string eventName = playable.eventName;
                if (!IsDonorVoice(eventName))
                    continue;
                playable.eventName = "";
                muted++;
            }
            if (muted > 0 && !_logged)
            {
                _logged = true;
                Plugin.Logger?.LogInfo("Muted " + muted + " donor FMOD voice tracks on Gsound appearance.");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Gsound appearance voice mute failed: " + ex.Message);
        }
    }

    private static bool Prefix_SetPlayVoice(BattleUnitView __instance)
    {
        if (__instance != null && StageFieldAccess.IsGsoundUnit(__instance.unitModel))
            return false;
        return true;
    }

    private static bool Prefix_SetPlayVoice_Instance(BattleUnitView __instance)
    {
        if (__instance != null && StageFieldAccess.IsGsoundUnit(__instance.unitModel))
            return false;
        return true;
    }

    private static bool Prefix(string __0, ref EventInstance __result)
    {
        if (!ShouldMute(__0))
            return true;
        __result = default(EventInstance);
        return false;
    }

    private static bool ShouldMute(string path)
    {
        if (!IsDonorVoice(path))
            return false;
        try
        {
            if (StageFieldAccess.HasGsoundRequest() || StageFieldAccess.StageHasGsound(null))
                return true;
        }
        catch
        {
        }
        return false;
    }

    private static bool IsDonorVoice(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        if (path.IndexOf("Voice", StringComparison.OrdinalIgnoreCase) < 0 &&
            path.IndexOf("voice", StringComparison.Ordinal) < 0)
            return false;
        return ContainsIdentity(path, "10916") ||
               ContainsIdentity(path, "10716") ||
               ContainsIdentity(path, "10710") ||
               ContainsIdentity(path, "8172") ||
               ContainsIdentity(path, "8173") ||
               ContainsIdentity(path, "8164") ||
               ContainsIdentity(path, "8174") ||
               ContainsIdentity(path, "8166") ||
               path.IndexOf("Heathcliff", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("Rodion", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsIdentity(string path, string id)
    {
        int index = path.IndexOf(id, StringComparison.Ordinal);
        while (index >= 0)
        {
            bool leftOk = index == 0 || !char.IsDigit(path[index - 1]);
            int end = index + id.Length;
            bool rightOk = end >= path.Length || !char.IsDigit(path[end]);
            if (leftOk && rightOk)
                return true;
            index = path.IndexOf(id, index + id.Length, StringComparison.Ordinal);
        }
        return false;
    }
 }

 internal static class BattleUnitView_InitSkinInRuntime_ThumbFx_Patch
 {
    static void Postfix(BattleUnitView __instance)
    {
        ChaosLineAccess.StripFrontOnView(__instance);
        GsoundVoiceMuteAccess.MuteAppearance(__instance);
        if (BlankDomainVisualAccess.IsActive(__instance))
            BlankDomainVisualAccess.Refresh(__instance);
        else
            ThumbActionEffectBindingAccess.Ensure(__instance);
        StageFieldAccess.TryPlayLatentBlood();
    }
 }

 internal static class BattleUnitView_RefreshAppearanceRenderer_ThumbFx_Patch
 {
    static void Postfix(BattleUnitView __instance)
    {
        ChaosLineAccess.StripFrontOnView(__instance);
        GsoundVoiceMuteAccess.MuteAppearance(__instance);
        if (BlankDomainVisualAccess.IsActive(__instance))
            BlankDomainVisualAccess.Refresh(__instance);
        else
            ThumbActionEffectBindingAccess.Ensure(__instance);
        StageFieldAccess.TryPlayLatentBlood();
    }
 }

 internal sealed class RefreshAbilityEffectConsequence : IModularConsequence
 {
    public void ExecuteConsequence(ModularSA modular, string functionName, string rawParams, string[] args)
    {
        if (args == null || args.Length < 1)
            return;

        var targets = modular.GetTargetModelList(args[0]);
        if (targets == null)
            return;

        var enumerator = targets.GetEnumerator();
        while (enumerator.MoveNext())
        {
            BattleUnitModel model = enumerator.Current;
            BattleUnitView view = BattleObjectManager.Instance.GetView(model);
            if (view == null)
                continue;

            try
            {
                // MindHeart uses official Thumb Father 心-不光彩 overlay
                // (BuffAbility_ShinEffect). Refresh the view so that ability
                // effect wakes on the 10916 appearance, and strip any leftover
                // T Corp Borrowed Time clone from older bridge versions.
                MindHeartFxAccess.Ensure(view, model);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning("Gsound ability-effect refresh failed: " + ex.Message);
            }
        }
    }
 }

 internal static class MindHeartFxAccess
 {
    private const string LegacyTCorpEffectName = "Gsound_MindHeart_Official_TimeRentalFx";
    private const string LegacyTCorpDonorEffectName = "FX_PC_7_Donquixote_TCorp_Noise";

    internal static void Ensure(BattleUnitView view, BattleUnitModel model)
    {
        if (view == null || view.Pointer == IntPtr.Zero)
            return;

        DestroyLegacyTCorpFx(view);

        try
        {
            if (model != null)
            {
                view.RefreshState(model, false, true);
                Plugin.Logger?.LogInfo("MindHeart FX: refreshed official ShinEffect overlay (心-不光彩)");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("MindHeart FX: RefreshState failed: " + ex);
        }
    }

    private static void DestroyLegacyTCorpFx(BattleUnitView view)
    {
        UnityEngine.Transform appearanceRoot = view.Appearance != null ? view.Appearance.transform : null;
        DestroyNamed(appearanceRoot, LegacyTCorpEffectName);
        DestroyNamed(appearanceRoot, LegacyTCorpDonorEffectName);
        DestroyNamed(view.transform, LegacyTCorpEffectName);
        DestroyNamed(view.transform, LegacyTCorpDonorEffectName);
        if (view.ViewEffectRootBack != null)
        {
            DestroyNamed(view.ViewEffectRootBack, LegacyTCorpEffectName);
            DestroyNamed(view.ViewEffectRootBack, LegacyTCorpDonorEffectName);
        }
    }

    private static void DestroyNamed(UnityEngine.Transform root, string name)
    {
        UnityEngine.Transform found = FindDeep(root, name);
        if (found == null || found.gameObject == null)
            return;
        Plugin.Logger?.LogInfo("MindHeart FX: removed leftover T Corp clone " + name);
        UnityEngine.Object.Destroy(found.gameObject);
    }

    private static UnityEngine.Transform FindDeep(UnityEngine.Transform root, string name)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, name, StringComparison.Ordinal))
            return root;
        for (int index = 0; index < root.childCount; index++)
        {
            UnityEngine.Transform match = FindDeep(root.GetChild(index), name);
            if (match != null)
                return match;
        }
        return null;
    }

    internal static void Clear()
    {
    }
 }

 internal sealed class GsoundBgmConsequence : IModularConsequence
 {
    private const string ShatteredDreamPath = "event:/BGM/Story/Shattered_Dream";
    private const string ShatteredDreamBank = "BGM_Story_S7_4.bank";
    private const string ShatteredDreamAssetsBank = "BGM_Story_S7_4.assets.bank";
    private static bool _active;
    private static bool _banksLoaded;
    private static int _restoreIndex;
    private static Bank _storyBank;
    private static Bank _storyAssetsBank;

    public void ExecuteConsequence(ModularSA modular, string functionName, string rawParams, string[] args)
    {
        if (args == null || args.Length < 1)
            return;

        string operation = args[0]?.Trim().ToLowerInvariant();
        try
        {
            if (operation == "start")
            {
                Start();
            }
            else if (operation == "stop")
            {
                Stop();
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Gsound BGM bridge failed: " + ex.Message);
        }
    }

    private static void Start()
    {
        // The unlock callback and White Paper's OnUse callback both request the
        // same two-round BGM window. Do not restart the song at skill entry.
        if (_active)
        {
            Plugin.Logger?.LogInfo("Gsound BGM: Shattered Dream is already active; keeping the current playback position");
            return;
        }

        SoundManager manager = SoundManager.Instance;
        if (manager != null)
        {
            try
            {
                EventInstance instance;
                if (!TryCreateShatteredDream(manager, out instance))
                {
                    EnsureShatteredDreamBanksLoaded();
                    TryCreateShatteredDream(manager, out instance);
                }

                if (instance.isValid())
                {
                    var currentBgm = manager.GetCurrentBGM();
                    if (currentBgm != null)
                    {
                        _restoreIndex = BattleSoundGenerator.CurrentBgmIndex;
                        currentBgm.ChangeBGM(instance, 1f);
                        _active = true;
                        Plugin.Logger?.LogInfo("Gsound BGM: switched to Shattered Dream from " + ShatteredDreamBank + ", restore index " + _restoreIndex);
                        return;
                    }
                }

                Plugin.Logger?.LogWarning("Gsound BGM: Shattered Dream is still invalid after loading " + ShatteredDreamBank);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning("Gsound BGM: Shattered Dream failed: " + ex);
            }
        }
    }

    private static bool TryCreateShatteredDream(SoundManager manager, out EventInstance instance)
    {
        instance = manager.CreateInstance(ShatteredDreamPath);
        return instance.isValid();
    }

    private static void EnsureShatteredDreamBanksLoaded()
    {
        if (_banksLoaded && _storyBank.isValid() && _storyAssetsBank.isValid())
            return;

        // SoundManager unloads scene-specific banks when leaving battle. The
        // bridge itself survives scene changes, so never trust a stale flag.
        _banksLoaded = false;

        string bankRoot = Path.Combine(
            UnityEngine.Application.streamingAssetsPath,
            "Assets",
            "Sound",
            "FMODBuilds",
            "Desktop");
        string assetsPath = Path.Combine(bankRoot, ShatteredDreamAssetsBank);
        string eventPath = Path.Combine(bankRoot, ShatteredDreamBank);

        if (!File.Exists(assetsPath) || !File.Exists(eventPath))
            throw new FileNotFoundException("Shattered Dream FMOD banks were not found under " + bankRoot);

        // Shattered_Dream is physically stored in BGM_Story_S7_4. The lobby
        // normally loads this bank, but battles unload it; load the sample bank
        // first, then the event metadata bank, directly into the game's studio
        // system so the event path becomes valid during combat.
        FMOD.RESULT assetsResult = RuntimeManager.StudioSystem.loadBankFile(
            assetsPath,
            LOAD_BANK_FLAGS.NORMAL,
            out _storyAssetsBank);
        FMOD.RESULT eventResult = RuntimeManager.StudioSystem.loadBankFile(
            eventPath,
            LOAD_BANK_FLAGS.NORMAL,
            out _storyBank);

        if (_storyAssetsBank.isValid())
            _storyAssetsBank.loadSampleData();
        if (_storyBank.isValid())
            _storyBank.loadSampleData();

        _banksLoaded = _storyBank.isValid();
        Plugin.Logger?.LogInfo(
            "Gsound BGM: loaded Shattered Dream banks (assets=" + assetsResult +
            ", event=" + eventResult + ", valid=" + _banksLoaded + ")");
    }

    private static void Stop()
    {
        if (!_active)
            return;

        BattleSoundGenerator.SetChangeBGM(_restoreIndex);
        _active = false;
        Plugin.Logger?.LogInfo("Gsound BGM: restored battle music to index " + _restoreIndex);
    }
 }

 internal static class BloodDinnerTraceAccess
 {
    internal const bool Enabled = false;
    private static int _sequence;

    internal static bool IsBloodDinner(string label)
    {
        return string.Equals(label, "BloodDinner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "BloodDinnerWave", StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "BloodDinnerUsage", StringComparison.OrdinalIgnoreCase);
    }

    internal static void Write(string message)
    {
        int sequence = System.Threading.Interlocked.Increment(ref _sequence);
        Plugin.Logger?.LogInfo("BDTRACE #" + sequence.ToString("D4") + " " + message);
    }

    internal static string DescribeView(BattleUnitView view)
    {
        if (view == null)
            return "view=<null>";
        try
        {
            BattleUnitModel model = view.unitModel;
            int origin = model == null ? -1 : model.GetOriginUnitID();
            return "view=" + view.GetInstanceID() + ",origin=" + origin +
                ",dir=" + view.UnitDirection + ",go=" + view.gameObject.name;
        }
        catch (Exception ex)
        {
            return "view=<describe failed:" + ex.Message + ">";
        }
    }

    internal static string DescribeEffect(Effect_Label effect)
    {
        if (effect == null)
            return "effect=<null>";
        try
        {
            UnityEngine.GameObject obj = effect.effectObj;
            if (obj == null)
                return "effectLabel=" + effect.label + ",effectObj=<null>";

            string parentPath = DescribeTransformPath(obj.transform);
            UnityEngine.Animator animator = obj.GetComponentInChildren<UnityEngine.Animator>(true);
            string animatorState = "anim=<none>";
            if (animator != null)
            {
                UnityEngine.AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                animatorState = "anim(enabled=" + animator.enabled +
                    ",active=" + animator.gameObject.activeInHierarchy +
                    ",speed=" + animator.speed +
                    ",state=" + state.fullPathHash +
                    ",time=" + state.normalizedTime.ToString("F3") + ")";
            }

            var particles = obj.GetComponentsInChildren<UnityEngine.ParticleSystem>(true);
            var particleSummary = new System.Text.StringBuilder();
            for (int i = 0; i < particles.Length; i++)
            {
                UnityEngine.ParticleSystem ps = particles[i];
                if (i > 0) particleSummary.Append('|');
                UnityEngine.ParticleSystemRenderer renderer = ps.GetComponent<UnityEngine.ParticleSystemRenderer>();
                particleSummary.Append(ps.gameObject.name)
                    .Append("{a=").Append(ps.gameObject.activeInHierarchy)
                    .Append(",l=").Append(ps.gameObject.layer)
                    .Append(",r=").Append(renderer != null && renderer.enabled)
                    .Append(",p=").Append(ps.isPlaying)
                    .Append(",e=").Append(ps.isEmitting)
                    .Append(",n=").Append(ps.particleCount).Append('}');
            }

            return "effectLabel=" + effect.label +
                ",obj=" + obj.name + "#" + obj.GetInstanceID() +
                ",activeSelf=" + obj.activeSelf +
                ",activeHierarchy=" + obj.activeInHierarchy +
                ",layer=" + obj.layer +
                ",pos=" + obj.transform.position +
                ",localPos=" + obj.transform.localPosition +
                ",scale=" + obj.transform.lossyScale +
                ",path=" + parentPath +
                "," + animatorState +
                ",particles=[" + particleSummary + "]";
        }
        catch (Exception ex)
        {
            return "effect=<describe failed:" + ex + ">";
        }
    }

    internal static string DescribeController(AbilityEffect_BloodDinner controller)
    {
        if (controller == null)
            return "controller=<null>";
        try
        {
            UnityEngine.GameObject obj = controller.gameObject;
            return "controller=" + obj.name + "#" + obj.GetInstanceID() +
                ",active=" + obj.activeInHierarchy +
                ",layer=" + obj.layer +
                ",path=" + DescribeTransformPath(obj.transform);
        }
        catch (Exception ex)
        {
            return "controller=<describe failed:" + ex.Message + ">";
        }
    }

    private static string DescribeTransformPath(UnityEngine.Transform transform)
    {
        if (transform == null)
            return "<null>";
        var names = new System.Collections.Generic.List<string>();
        UnityEngine.Transform current = transform;
        int guard = 0;
        while (current != null && guard++ < 12)
        {
            names.Add(current.gameObject.name + "(L" + current.gameObject.layer + ")");
            current = current.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }
 }

 internal static class BloodDinnerTrace_GetGlobalLabel_Patch
 {
    static void Prefix(string label)
    {
        if (BloodDinnerTraceAccess.IsBloodDinner(label))
            BloodDinnerTraceAccess.Write("BattleEffectManager.GetEffect_Label PRE label=" + label);
    }

    static void Postfix(string label, Effect_Label __result)
    {
        if (BloodDinnerTraceAccess.IsBloodDinner(label))
            BloodDinnerTraceAccess.Write("BattleEffectManager.GetEffect_Label POST " +
                BloodDinnerTraceAccess.DescribeEffect(__result));
    }
 }

 internal static class BloodDinnerTrace_SetViewLabel_Patch
 {
    static void Prefix(BattleUnitView __instance, string label, bool active,
        EFFECT_LAYER_TYPE layerType, bool isSetOverrideDie, bool isCenter,
        float scale, bool isAddScript)
    {
        if (!BloodDinnerTraceAccess.IsBloodDinner(label)) return;
        BloodDinnerTraceAccess.Write("BattleUnitView.SetEffect_Label PRE label=" + label +
            ",active=" + active + ",layerType=" + layerType +
            ",overrideDie=" + isSetOverrideDie + ",center=" + isCenter +
            ",scale=" + scale + ",addScript=" + isAddScript + "," +
            BloodDinnerTraceAccess.DescribeView(__instance));
    }

    static void Postfix(BattleUnitView __instance, string label, Effect_Label __result)
    {
        if (!BloodDinnerTraceAccess.IsBloodDinner(label)) return;
        BloodDinnerTraceAccess.Write("BattleUnitView.SetEffect_Label POST " +
            BloodDinnerTraceAccess.DescribeView(__instance) + "," +
            BloodDinnerTraceAccess.DescribeEffect(__result));
    }
 }

 internal static class BloodDinnerTrace_SetActive_Patch
 {
    static void Prefix(Effect_Label __instance, bool isActive)
    {
        if (__instance == null || !BloodDinnerTraceAccess.IsBloodDinner(__instance.label)) return;
        BloodDinnerTraceAccess.Write("Effect_Label.SetActiveEffect PRE active=" + isActive + "," +
            BloodDinnerTraceAccess.DescribeEffect(__instance));
    }

    static void Postfix(Effect_Label __instance, bool isActive)
    {
        if (__instance == null || !BloodDinnerTraceAccess.IsBloodDinner(__instance.label)) return;
        BloodDinnerTraceAccess.Write("Effect_Label.SetActiveEffect POST active=" + isActive + "," +
            BloodDinnerTraceAccess.DescribeEffect(__instance));
    }
 }

 internal static class BloodDinnerTrace_SetEffect_Patch
 {
    static void Prefix(AbilityEffect_BloodDinner __instance, int curValue, int maxValue)
    {
        BloodDinnerTraceAccess.Write("AbilityEffect_BloodDinner.SetEffect PRE " +
            curValue + "/" + maxValue + "," + BloodDinnerTraceAccess.DescribeController(__instance));
    }

    static void Postfix(AbilityEffect_BloodDinner __instance, int curValue, int maxValue)
    {
        BloodDinnerTraceAccess.Write("AbilityEffect_BloodDinner.SetEffect POST " +
            curValue + "/" + maxValue + "," + BloodDinnerTraceAccess.DescribeController(__instance));
    }
 }

 internal static class BloodDinnerTrace_SetDinnerWave_Patch
 {
    static void Prefix(AbilityEffect_BloodDinner __instance,
        Il2CppSystem.Collections.Generic.List<BattleUnitView> unitList, float targetTime)
    {
        BloodDinnerTraceAccess.Write("AbilityEffect_BloodDinner.SetDinnerWave PRE units=" +
            (unitList == null ? -1 : unitList.Count) + ",targetTime=" + targetTime + "," +
            BloodDinnerTraceAccess.DescribeController(__instance));
    }

    static void Postfix(AbilityEffect_BloodDinner __instance,
        Il2CppSystem.Collections.Generic.List<BattleUnitView> unitList, float targetTime)
    {
        BloodDinnerTraceAccess.Write("AbilityEffect_BloodDinner.SetDinnerWave POST units=" +
            (unitList == null ? -1 : unitList.Count) + ",targetTime=" + targetTime + "," +
            BloodDinnerTraceAccess.DescribeController(__instance));
    }
 }

 internal static class BloodDinnerTrace_RefreshState_Patch
 {
    static void Prefix(AbilityEffect_BloodDinner __instance, int value)
    {
        BloodDinnerTraceAccess.Write("AbilityEffect_BloodDinner.SetEffect_RefreshState PRE value=" +
            value + "," + BloodDinnerTraceAccess.DescribeController(__instance));
    }

    static void Postfix(AbilityEffect_BloodDinner __instance, int value)
    {
        BloodDinnerTraceAccess.Write("AbilityEffect_BloodDinner.SetEffect_RefreshState POST value=" +
            value + "," + BloodDinnerTraceAccess.DescribeController(__instance));
    }
 }

 internal static class BloodDinnerTrace_UnitScriptInit_Patch
 {
    static void Prefix(UnitScript_BloodDinner __instance, CombatUnitModel unit, BattleUnitView view)
    {
        BloodDinnerTraceAccess.Write("UnitScript_BloodDinner.Init_After PRE script=" +
            __instance.Pointer + "," + BloodDinnerTraceAccess.DescribeView(view) +
            ",existing=" + BloodDinnerTraceAccess.DescribeEffect(
                view == null ? null : view.GetEffect_Label("BloodDinner")));
    }

    static void Postfix(UnitScript_BloodDinner __instance, CombatUnitModel unit, BattleUnitView view)
    {
        BloodDinnerTraceAccess.Write("UnitScript_BloodDinner.Init_After POST script=" +
            __instance.Pointer + "," + BloodDinnerTraceAccess.DescribeView(view) +
            ",registered=" + BloodDinnerTraceAccess.DescribeEffect(
                view == null ? null : view.GetEffect_Label("BloodDinner")));
    }
 }

 internal static class BloodDinnerTrace_AddStack_Patch
 {
    static void Prefix(int stack, BATTLE_EVENT_TIMING timing, bool addFromAbility)
    {
        BloodDinnerTraceAccess.Write("BloodDinnerBuff.AddStack PRE amount=" + stack +
            ",timing=" + timing + ",fromAbility=" + addFromAbility);
    }

    static void Postfix(int stack, BATTLE_EVENT_TIMING timing, bool addFromAbility, int __result)
    {
        BloodDinnerTraceAccess.Write("BloodDinnerBuff.AddStack POST amount=" + stack +
            ",result=" + __result + ",timing=" + timing + ",fromAbility=" + addFromAbility);
    }
 }

 internal static class StageFieldAccess
 {
    private const int GsoundPersonalityId = 107970;
    private static bool _reportedMissingUi;
    private static bool _gsoundRequested;
    private static bool _refreshingUi;
    private static bool _refreshingBloodFx;
    private static IntPtr _gsoundManagerPointer;
    private static Effect_Label _bloodDinnerFieldEffect;
    private static int _bloodDinnerVisualStack = -1;
    private static readonly HashSet<IntPtr> _bloodDinnerUnitEffectsInitialized = new HashSet<IntPtr>();
    private static BattleEffectList _bloodDinnerEffectList;
    private static AsyncOperationHandle<BattleEffectList> _bloodDinnerEffectListHandle;

    internal static bool HasGsoundRequest()
    {
        return _gsoundRequested;
    }

    internal static void ActivateGsound()
    {
        _gsoundRequested = true;
        StageBuffManager manager = BattleUnitBuffManager.StageBuffManager;
        _gsoundManagerPointer = manager == null ? IntPtr.Zero : manager.Pointer;
    }

    internal static void ResetGsoundActivation()
    {
        try
        {
            if (_bloodDinnerFieldEffect != null)
                _bloodDinnerFieldEffect.SetActiveEffect(false);
        }
        catch
        {
        }
        _bloodDinnerFieldEffect = null;
        _bloodDinnerVisualStack = -1;
        _bloodDinnerUnitEffectsInitialized.Clear();
        _gsoundRequested = false;
        _gsoundManagerPointer = IntPtr.Zero;
    }

    internal static bool IsGsoundUnit(BattleUnitModel unit)
    {
        if (unit == null)
            return false;
        try
        {
            if (unit.GetOriginUnitID() == GsoundPersonalityId)
                return true;
        }
        catch
        {
        }
        return false;
    }

    internal static bool StageHasGsound(StageModel model)
    {
        try
        {
            if (model != null && model.IsInclusivePersonality(GsoundPersonalityId))
                return true;
        }
        catch
        {
        }

        try
        {
            BattleObjectManager objects = BattleObjectManager.Instance;
            if (objects == null)
                return false;
            if (ListHasGsound(objects.GetAliveList(true, UNIT_FACTION.PLAYER)))
                return true;
            if (ListHasGsound(objects.GetAliveList(true, UNIT_FACTION.ENEMY)))
                return true;
        }
        catch
        {
        }
        return false;
    }

    private static bool ListHasGsound(Il2CppSystem.Collections.Generic.List<BattleUnitModel> units)
    {
        if (units == null)
            return false;
        for (int i = 0; i < units.Count; i++)
        {
            if (IsGsoundUnit(units[i]))
                return true;
        }
        return false;
    }

    internal static void TryPlayLatentBlood()
    {
        if (_refreshingBloodFx || (!_gsoundRequested && !StageHasGsound(null)))
            return;

        _refreshingBloodFx = true;
        try
        {
            BattleObjectManager objects = BattleObjectManager.Instance;
            BattleUnitModel unit = FindAnyGsoundUnit();
            BattleUnitView view = objects == null || unit == null ? null : objects.GetView(unit);
            if (view == null)
                return;

            if (!EnsureBloodDinnerEffectList())
                return;

            // BloodDinner has two cooperating pieces. BattleMapManager owns
            // the battlefield animator, while UnitScript_BloodDinner creates
            // BloodDinnerWave/BloodDinnerUsage on the participating unit.
            // Custom identities may finish Init_After before the S5 effect
            // list exists, so replay it once after that list is available.
            EnsureBloodDinnerUnitEffects(unit, view);

            BattleMapManager map = BattleMapManager.Instance;
            if (map == null)
                return;

            bool needsCreate = _bloodDinnerFieldEffect == null
                || _bloodDinnerFieldEffect.effectObj == null;
            if (needsCreate)
            {
                try
                {
                    if (_bloodDinnerFieldEffect != null)
                        _bloodDinnerFieldEffect.SetActiveEffect(false);
                }
                catch
                {
                }

                // The original field is owned by BattleMapManager, not a unit
                // view. SetEffect_Label parents it to BattleMapObject/EffectPivot
                // and performs the same activation/registration as Bloodfiends.
                _bloodDinnerFieldEffect = map.SetEffect_Label("BloodDinner", true);
                _bloodDinnerVisualStack = -1;
            }

            if (_bloodDinnerFieldEffect == null)
            {
                Plugin.Logger?.LogWarning(
                    "Gsound Bloodfeast field: BattleMapManager could not create BloodDinner");
                return;
            }

            AbilityEffect_BloodDinner controller =
                _bloodDinnerFieldEffect.GetEffectScript<AbilityEffect_BloodDinner>();
            if (controller == null && _bloodDinnerFieldEffect.effectObj != null)
                controller = _bloodDinnerFieldEffect.effectObj
                    .GetComponentInChildren<AbilityEffect_BloodDinner>(true);
            if (controller == null)
            {
                Plugin.Logger?.LogWarning(
                    "Gsound Bloodfeast field: BloodDinner label has no AbilityEffect_BloodDinner controller");
                return;
            }

            int current = GetStack(BUFF_UNIQUE_KEYWORD.BloodDinner);
            // In the custom-only lineup the original controller remains at
            // targetTime=0 even after SetEffect/SetEffect_RefreshState. The
            // traced official call supplies current BloodDinner / 400 to the
            // unit waves, so reproduce that exact missing transition here.
            float beforeCurTime = controller._curTime;
            float beforeTargetTime = controller._targetTime;
            controller.SetEffect(current, 999);
            float visualTime = UnityEngine.Mathf.Clamp(current / 400f, 0f, 1f);
            controller._targetTime = visualTime;
            controller.SetDinnerWave(CollectBattleViews(), visualTime);
            if (_bloodDinnerVisualStack != current)
            {
                bool animatorReady = controller._curAnim != null;
                Plugin.Logger?.LogInfo(
                    "Gsound Bloodfeast field: synchronized map-owned BloodDinner visual " +
                    _bloodDinnerVisualStack + " -> " + current +
                    " (wave=" + (current / 400f) +
                    ", animator=" + animatorReady +
                    ", time=" + beforeCurTime + "->" + controller._curTime +
                    ", target=" + beforeTargetTime + "->" + controller._targetTime +
                    ", tier=" + controller._tier +
                    ", active=" + (_bloodDinnerFieldEffect.effectObj != null &&
                        _bloodDinnerFieldEffect.effectObj.activeInHierarchy) + ")");
                _bloodDinnerVisualStack = current;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Gsound Bloodfeast field FX failed: " + ex);
        }
        finally
        {
            _refreshingBloodFx = false;
        }
    }

    private static void EnsureBloodDinnerUnitEffects(BattleUnitModel unit, BattleUnitView view)
    {
        if (unit == null || view == null)
            return;

        try
        {
            UnitScript_BloodDinner script;
            if (!unit.TryGetUnitScriptByType<UnitScript_BloodDinner>(out script) || script == null)
            {
                Plugin.Logger?.LogWarning(
                    "Gsound Bloodfeast field: Gsound has no live UnitScript_BloodDinner instance");
                return;
            }

            Effect_Label wave = view.GetEffect_Label("BloodDinnerWave");
            bool ready = wave != null && wave.effectObj != null && script.DinnerWave != null;
            if (ready)
                return;
            if (_bloodDinnerUnitEffectsInitialized.Contains(script.Pointer))
                return;

            script.Init_After(unit, view);
            _bloodDinnerUnitEffectsInitialized.Add(script.Pointer);

            wave = view.GetEffect_Label("BloodDinnerWave");
            ready = wave != null && wave.effectObj != null && script.DinnerWave != null;
            Plugin.Logger?.LogInfo(
                "Gsound Bloodfeast field: initialized original unit wave/usage effects, ready=" + ready);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning(
                "Gsound Bloodfeast field: unit wave/usage initialization failed: " + ex);
        }
    }

    private static bool EnsureBloodDinnerEffectList()
    {
        BattleEffectManager effects = BattleEffectManager.Instance;
        if (effects == null)
            return false;

        try
        {
            if (effects.GetEffect_LabelSource("BloodDinner") != null)
                return true;

            if (_bloodDinnerEffectList == null)
            {
                _bloodDinnerEffectListHandle =
                    Addressables.LoadAssetAsync<BattleEffectList>("EffectList_SEASON_5");
                _bloodDinnerEffectList = _bloodDinnerEffectListHandle.WaitForCompletion();
                if (_bloodDinnerEffectList == null)
                {
                    Plugin.Logger?.LogWarning(
                        "Gsound Bloodfeast field: failed to load EffectList_SEASON_5");
                    return false;
                }
            }

            var nextLists = effects.battleEffectLists_next;
            if (nextLists == null)
            {
                nextLists = new Il2CppSystem.Collections.Generic.List<BattleEffectList>();
                effects.battleEffectLists_next = nextLists;
            }

            bool alreadyAdded = false;
            for (int i = 0; i < nextLists.Count; i++)
            {
                BattleEffectList item = nextLists[i];
                if (item != null && item.Pointer == _bloodDinnerEffectList.Pointer)
                {
                    alreadyAdded = true;
                    break;
                }
            }
            if (!alreadyAdded)
                nextLists.Add(_bloodDinnerEffectList);

            bool available = effects.GetEffect_LabelSource("BloodDinner") != null;
            int merged = 0;
            if (!available && effects.battleEffectList != null
                && _bloodDinnerEffectList.Effects_Label != null)
            {
                var targetLabels = effects.battleEffectList.Effects_Label;
                if (targetLabels == null)
                {
                    targetLabels = new Il2CppSystem.Collections.Generic.List<Effect_Label>();
                    effects.battleEffectList.Effects_Label = targetLabels;
                }

                var sourceLabels = _bloodDinnerEffectList.Effects_Label;
                for (int i = 0; i < sourceLabels.Count; i++)
                {
                    Effect_Label source = sourceLabels[i];
                    if (source == null
                        || (source.label != "BloodDinner"
                            && source.label != "BloodDinnerWave"
                            && source.label != "BloodDinnerUsage"))
                        continue;

                    bool found = false;
                    for (int j = 0; j < targetLabels.Count; j++)
                    {
                        Effect_Label target = targetLabels[j];
                        if (target != null && target.label == source.label)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        targetLabels.Add(source);
                        merged++;
                    }
                }
                available = effects.GetEffect_LabelSource("BloodDinner") != null;
            }
            Plugin.Logger?.LogInfo(
                "Gsound Bloodfeast field: loaded EffectList_SEASON_5, merged=" + merged +
                ", label available=" + available);
            return available;
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning(
                "Gsound Bloodfeast field: EffectList_SEASON_5 load failed: " + ex);
            return false;
        }
    }

    internal static void RefreshMapBloodVisual()
    {
        TryPlayLatentBlood();
    }

    internal static BattleUnitModel FindAnyGsoundUnit()
    {
        BattleObjectManager objects = BattleObjectManager.Instance;
        if (objects == null)
            return null;
        BattleUnitModel found = FindGsoundInList(objects.GetAliveList(true, UNIT_FACTION.PLAYER));
        if (found != null)
            return found;
        return FindGsoundInList(objects.GetAliveList(true, UNIT_FACTION.ENEMY));
    }

    private static BattleUnitModel FindGsoundInList(
        Il2CppSystem.Collections.Generic.List<BattleUnitModel> units)
    {
        if (units == null)
            return null;
        for (int i = 0; i < units.Count; i++)
        {
            if (IsGsoundUnit(units[i]))
                return units[i];
        }
        return null;
    }

    private static Il2CppSystem.Collections.Generic.List<BattleUnitView> CollectBattleViews()
    {
        var views = new Il2CppSystem.Collections.Generic.List<BattleUnitView>();
        BattleObjectManager objects = BattleObjectManager.Instance;
        if (objects == null)
            return views;
        AppendBattleViews(objects, objects.GetAliveList(true, UNIT_FACTION.PLAYER), views);
        AppendBattleViews(objects, objects.GetAliveList(true, UNIT_FACTION.ENEMY), views);
        return views;
    }

    private static void AppendBattleViews(
        BattleObjectManager objects,
        Il2CppSystem.Collections.Generic.List<BattleUnitModel> units,
        Il2CppSystem.Collections.Generic.List<BattleUnitView> views)
    {
        if (objects == null || units == null || views == null)
            return;
        for (int i = 0; i < units.Count; i++)
        {
            BattleUnitModel unit = units[i];
            BattleUnitView view = objects.GetView(unit);
            if (view != null)
                views.Add(view);
        }
    }

    internal static void TryPlayStackAddingEffect(BattleUnitModel unit)
    {
        if (unit == null)
            return;
        try
        {
            BloodDinnerBuff.PlayStackAddingEffect(unit);
            Plugin.Logger?.LogInfo("Gsound Bloodfeast: played official stack/field FX");
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Gsound Bloodfeast stack FX failed: " + ex.Message);
        }
    }

    internal static bool IsGsoundActive(StageBuffManager manager)
    {
        if (!_gsoundRequested || manager == null)
            return false;

        if (_gsoundManagerPointer == IntPtr.Zero)
            _gsoundManagerPointer = manager.Pointer;

        return _gsoundManagerPointer == manager.Pointer;
    }

    internal static bool TryGetLacerationDamage(
        StageBuffManager manager,
        object[] args,
        out int damage,
        out BATTLE_EVENT_TIMING timing)
    {
        damage = 0;
        timing = BATTLE_EVENT_TIMING.NOT_PRINT;
        if (!IsGsoundActive(manager) || args == null || args.Length < 8)
            return false;

        object keyword = args[7];
        bool isLaceration = keyword is BUFF_UNIQUE_KEYWORD lacerationKeyword
            && lacerationKeyword == BUFF_UNIQUE_KEYWORD.Laceration;
        if (!isLaceration && !string.Equals(keyword?.ToString(), "Laceration", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            damage = Math.Max(0, Convert.ToInt32(args[1]));
            if (damage == 0)
                damage = Math.Max(0, Convert.ToInt32(args[2]));
        }
        catch
        {
            damage = 0;
        }

        if (args[3] is BATTLE_EVENT_TIMING typedTiming)
            timing = typedTiming;

        return damage > 0;
    }

    internal static bool TryResolve(string fieldName, out BUFF_UNIQUE_KEYWORD keyword)
    {
        if (string.Equals(fieldName, "blooddinner", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldName, "bloodfeast", StringComparison.OrdinalIgnoreCase))
        {
            keyword = BUFF_UNIQUE_KEYWORD.BloodDinner;
            return true;
        }

        if (string.Equals(fieldName, "firefield", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldName, "scorchfield", StringComparison.OrdinalIgnoreCase))
        {
            keyword = BUFF_UNIQUE_KEYWORD.FireField;
            return true;
        }

        keyword = default(BUFF_UNIQUE_KEYWORD);
        return false;
    }

    internal static StageBuffModel Ensure(BUFF_UNIQUE_KEYWORD keyword)
    {
        StageBuffManager manager = BattleUnitBuffManager.StageBuffManager;
        if (manager == null)
            return null;

        if (_gsoundRequested && _gsoundManagerPointer == IntPtr.Zero)
            _gsoundManagerPointer = manager.Pointer;

        if (!manager.CheckStageBuff(keyword))
            manager.AddStageBuff(keyword, true);

        if (keyword == BUFF_UNIQUE_KEYWORD.BloodDinner)
            TryPlayLatentBlood();

        return keyword == BUFF_UNIQUE_KEYWORD.BloodDinner
            ? (StageBuffModel)BloodDinnerBuff.BuffInstance
            : (StageBuffModel)FireFieldBuff.BuffInstance;
    }

    internal static void EnsureBoth()
    {
        Ensure(BUFF_UNIQUE_KEYWORD.BloodDinner);
        Ensure(BUFF_UNIQUE_KEYWORD.FireField);
    }

    internal static StageBuffUI GetOrCreateUi(BattleUI.WaveUI wave)
    {
        if (wave == null)
            return null;

        StageBuffUI ui = wave._stageBuffUI;
        if (ui != null)
            return ui;

        try
        {
            StageBuffUI ori = wave._stageBuffUIOri;
            UnityEngine.Transform parent = wave._stageBuffParentRect;
            if (ori == null || parent == null)
                return null;

            ui = UnityEngine.Object.Instantiate(ori, parent);
            StripWorldPlaceholders(ui.gameObject);
            ui.gameObject.SetActive(true);
            wave._stageBuffUI = ui;
            Plugin.Logger?.LogInfo("Created StageBuffUI under the official parent rect.");
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("GetOrCreateUi failed: " + ex.Message);
            return null;
        }

        return ui;
    }

    private static void StripWorldPlaceholders(UnityEngine.GameObject root)
    {
        if (root == null)
            return;
        UnityEngine.MeshRenderer[] renderers = root.GetComponentsInChildren<UnityEngine.MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }
        // Do not deactivate MeshFilter objects: the official click target lives
        // on those GOs. Hiding the leftover cube is renderer-only.
    }

    internal static void InjectIntoStageBuffUI(StageBuffUI ui)
    {
        if (ui == null) return;
        try
        {
            StripWorldPlaceholders(ui.gameObject);
            var list = new Il2CppSystem.Collections.Generic.List<BUFF_UNIQUE_KEYWORD>();
            list.Add(BUFF_UNIQUE_KEYWORD.BloodDinner);
            list.Add(BUFF_UNIQUE_KEYWORD.FireField);
            ui.SetData(list);
            ui.SetActive(true);
            ui.gameObject.SetActive(true);
            EnableStageBuffClicks(ui);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("InjectIntoStageBuffUI failed: " + ex.Message);
        }
    }

    private static bool _loggedClickWire;

    private static void EnableStageBuffClicks(StageBuffUI ui)
    {
        if (ui == null || ui.gameObject == null)
            return;

        UnityEngine.CanvasGroup[] groups = ui.GetComponentsInChildren<UnityEngine.CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null)
                continue;
            groups[i].interactable = true;
            groups[i].blocksRaycasts = true;
        }

        UnityEngine.UI.Graphic[] graphics = ui.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = true;
        }

        UnityEngine.Collider[] colliders = ui.GetComponentsInChildren<UnityEngine.Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = true;
        }

        UnityEngine.Collider2D[] colliders2d = ui.GetComponentsInChildren<UnityEngine.Collider2D>(true);
        for (int i = 0; i < colliders2d.Length; i++)
        {
            if (colliders2d[i] != null)
                colliders2d[i].enabled = true;
        }

        UnityEngine.UI.Button[] buttons = ui.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            UnityEngine.UI.Button button = buttons[i];
            if (button == null)
                continue;
            button.interactable = true;
            button.enabled = true;
            button.gameObject.SetActive(true);
        }

        if (!_loggedClickWire)
        {
            _loggedClickWire = true;
            var tooltipSetter = AccessTools.Method(typeof(StageBuffUI), "SetActiveStageBuffTooltip");
            if (tooltipSetter != null)
            {
                try
                {
                    tooltipSetter.Invoke(ui, new object[] { true });
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogWarning("SetActiveStageBuffTooltip failed: " + ex.Message);
                }
            }
            Plugin.Logger?.LogInfo(
                "StageBuffUI click enable: buttons=" + buttons.Length +
                " graphics=" + graphics.Length +
                " canvasGroups=" + groups.Length +
                " colliders=" + colliders.Length);
        }
    }

    internal static void RefreshUi()
    {
        if (_refreshingUi)
            return;
        _refreshingUi = true;
        try
        {
            BattleUI.BattleUIRoot root = BattleUI.BattleUIRoot.Instance;
            if (root == null || root.BattleBasicUIController == null)
                return;

            BattleUI.WaveUI wave = root.BattleBasicUIController.WaveUIController;
            if (wave == null)
                return;

            EnsureBoth();
            StageBuffUI ui = GetOrCreateUi(wave);
            InjectIntoStageBuffUI(ui);
            try
            {
                wave.CheckActiveStageBuffUI();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("CheckActiveStageBuffUI after create failed: " + ex.Message);
            }
            try
            {
                root.UpdateStageBuffUIOnStartRound();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("UpdateStageBuffUIOnStartRound failed: " + ex.Message);
            }
            ui = GetOrCreateUi(wave);
            InjectIntoStageBuffUI(ui);
            if (ui == null && !_reportedMissingUi)
            {
                Plugin.Logger.LogWarning("StageBuff models exist, but WaveUI has no StageBuffUI prefab to instantiate.");
                _reportedMissingUi = true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("Could not refresh StageBuffUI: " + ex.Message);
        }
        finally
        {
            _refreshingUi = false;
        }
    }

    internal static int GetStack(BUFF_UNIQUE_KEYWORD keyword)
    {
        StageBuffManager manager = BattleUnitBuffManager.StageBuffManager;
        return manager == null ? 0 : manager.GetCurrentStack(keyword);
    }
 }

 internal static class WaveUI_CheckActiveStageBuffUI_Patch
 {
    static void Prefix(BattleUI.WaveUI __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!StageFieldAccess.IsGsoundActive(BattleUnitBuffManager.StageBuffManager) && !StageFieldAccess.HasGsoundRequest())
                return;
            StageFieldAccess.EnsureBoth();
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("WaveUI patch prefix failed: " + ex.Message);
        }
    }

    static void Postfix(BattleUI.WaveUI __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!StageFieldAccess.HasGsoundRequest()) return;
            StageFieldAccess.EnsureBoth();
            StageBuffUI ui = StageFieldAccess.GetOrCreateUi(__instance);
            StageFieldAccess.InjectIntoStageBuffUI(ui);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("WaveUI patch postfix failed: " + ex.Message);
        }
    }
 }

 internal static class BattleUIRoot_UpdateStageBuffUIOnStartRound_Patch
 {
    static void Postfix()
    {
        if (!StageFieldAccess.HasGsoundRequest())
            return;
        StageFieldAccess.RefreshUi();
    }
 }

 [HarmonyPatch(typeof(StageBuffUI), "SetData")]
 internal static class StageBuffUI_SetData_Patch
 {
    static void Prefix(ref Il2CppSystem.Collections.Generic.List<BUFF_UNIQUE_KEYWORD> validBuffKeywordList)
    {
        try
        {
            if (validBuffKeywordList == null) return;
            if (!StageFieldAccess.HasGsoundRequest()) return;
            var mgr = BattleUnitBuffManager.StageBuffManager;
            if (mgr == null) return;
            if (!validBuffKeywordList.Contains(BUFF_UNIQUE_KEYWORD.BloodDinner))
                validBuffKeywordList.Add(BUFF_UNIQUE_KEYWORD.BloodDinner);
            if (!validBuffKeywordList.Contains(BUFF_UNIQUE_KEYWORD.FireField))
                validBuffKeywordList.Add(BUFF_UNIQUE_KEYWORD.FireField);
        }
        catch { }
    }
 }

 [HarmonyPatch(typeof(StageBuffManager), "Clear")]
 internal static class StageBuffManager_Clear_Patch
 {
    static void Postfix()
    {
        StageFieldAccess.ResetGsoundActivation();
        MindHeartFxAccess.Clear();
        BlackFogAccess.Clear();
        RelocationFieldAccess.Clear();
        ChaosLineAccess.Clear();
        GsoundRecoverMotionAccess.Clear();
        ThumbActionEffectBindingAccess.Clear();
        // This callback runs at round boundaries. Keep the committed Blank
        // Domain form, presentation registry, and extracted FX holders until
        // gsoundmotion(reset) explicitly ends the encounter. Clearing these
        // here destroys the phase-two FX between rounds before the next
        // timeline has a chance to rebind its references.
    }
 }

 [HarmonyPatch(typeof(StageBuffManager), "OnTakeHpDamage")]
 internal static class StageBuffManager_OnTakeHpDamage_Patch
 {
    private static bool _track;
    private static int _beforeBloodDinner;
    private static int _lacerationDamage;
    private static BATTLE_EVENT_TIMING _timing;
    private static BattleUnitModel _lacerationUnit;

    static void Prefix(StageBuffManager __instance, object[] __args)
    {
        _lacerationUnit = null;
        _track = StageFieldAccess.TryGetLacerationDamage(
            __instance,
            __args,
            out _lacerationDamage,
            out _timing);
        if (_track)
        {
            _beforeBloodDinner = StageFieldAccess.GetStack(BUFF_UNIQUE_KEYWORD.BloodDinner);
            if (__args != null && __args.Length > 0)
                _lacerationUnit = __args[0] as BattleUnitModel;
        }
    }

    static void Postfix(StageBuffManager __instance)
    {
        if (!_track)
            return;

        _track = false;
        int afterBloodDinner = StageFieldAccess.GetStack(BUFF_UNIQUE_KEYWORD.BloodDinner);
        // If an official Bloodfeast owner is present, the original game code
        // has already added this damage.  Only fill the gap for Gsound's
        // independent passive, so pairing it with a bloodfiend never doubles
        // the shared BloodDinner resource.
        if (afterBloodDinner > _beforeBloodDinner)
            return;

        StageBuffModel field = StageFieldAccess.Ensure(BUFF_UNIQUE_KEYWORD.BloodDinner);
        if (field == null)
            return;

        field.AddStack(_lacerationDamage, _timing, false);
        StageFieldAccess.TryPlayStackAddingEffect(_lacerationUnit);
        StageFieldAccess.RefreshUi();
        Plugin.Logger?.LogInfo("Gsound Bloodfeast: added " + _lacerationDamage + " from Laceration damage");
    }
 }

 internal static class StageBuffManager_CheckBloodDinnerOnInitStage_Patch
 {
    static void Postfix(StageBuffManager __instance, StageModel model)
    {
        try
        {
            if (__instance == null)
                return;
            if (!StageFieldAccess.StageHasGsound(model))
                return;
            StageFieldAccess.ActivateGsound();
            if (!__instance.CheckStageBuff(BUFF_UNIQUE_KEYWORD.BloodDinner))
                __instance.AddStageBuff(BUFF_UNIQUE_KEYWORD.BloodDinner, true);
            StageFieldAccess.TryPlayLatentBlood();
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("CheckBloodDinnerOnInitStage postfix failed: " + ex.Message);
        }
    }
 }

 internal static class StageBuffManager_CheckUnitModelUseBuff_Patch
 {
    static void Postfix(BUFF_UNIQUE_KEYWORD keyword, BattleUnitModel target, ref bool __result)
    {
        if (__result)
            return;
        if (keyword != BUFF_UNIQUE_KEYWORD.BloodDinner)
            return;
        if (StageFieldAccess.IsGsoundUnit(target))
            __result = true;
    }
 }

 internal static class StageBuffManager_CheckUnitScriptInherits_Patch
 {
    static void Postfix(Il2CppSystem.Type targetClassType, string unitScriptId, ref bool __result)
    {
        if (__result)
            return;
        if (!string.Equals(unitScriptId, "BloodDinner", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(unitScriptId, "107970", StringComparison.Ordinal))
            return;
        string typeName = targetClassType == null ? string.Empty : targetClassType.Name;
        if (typeName.IndexOf("BloodDinner", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("AddingBloodDinner", StringComparison.OrdinalIgnoreCase) >= 0)
            __result = true;
    }
 }

 internal static class BloodDinnerBuff_AddStack_MapFx_Patch
 {
    static void Postfix()
    {
        if (!StageFieldAccess.HasGsoundRequest())
            return;
        StageFieldAccess.RefreshMapBloodVisual();
    }
 }

 internal static class StageBuffManager_OnWaveStart_BloodFx_Patch
 {
    static void Postfix()
    {
        try
        {
            if (!StageFieldAccess.HasGsoundRequest() && !StageFieldAccess.StageHasGsound(null))
                return;
            StageFieldAccess.ActivateGsound();
            StageFieldAccess.Ensure(BUFF_UNIQUE_KEYWORD.BloodDinner);
            StageFieldAccess.TryPlayLatentBlood();
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("OnWaveStart blood FX failed: " + ex.Message);
        }
    }
 }

 internal sealed class StageFieldConsequence : IModularConsequence
 {
    public void ExecuteConsequence(ModularSA modular, string functionName, string rawParams, string[] args)
    {
        if (args == null || args.Length < 1)
            return;

        string fieldName = args[0]?.Trim();
        if (string.Equals(fieldName, "activate", StringComparison.OrdinalIgnoreCase))
        {
            StageFieldAccess.ActivateGsound();
            return;
        }
        if (string.Equals(fieldName, "deactivate", StringComparison.OrdinalIgnoreCase))
        {
            StageFieldAccess.ResetGsoundActivation();
            return;
        }

        BUFF_UNIQUE_KEYWORD keyword;
        if (args.Length < 2 || !StageFieldAccess.TryResolve(fieldName, out keyword))
            return;

        string operation = args[1]?.Trim().ToLowerInvariant();
        if (operation == "ensure")
        {
            StageFieldAccess.Ensure(keyword);
            if (keyword == BUFF_UNIQUE_KEYWORD.BloodDinner)
                StageFieldAccess.TryPlayLatentBlood();
            else
                StageFieldAccess.EnsureBoth();
            StageFieldAccess.RefreshUi();
            return;
        }
        if (operation == "mapfx")
        {
            StageFieldAccess.Ensure(keyword);
            StageFieldAccess.TryPlayLatentBlood();
            StageFieldAccess.RefreshUi();
            return;
        }

        if (args.Length < 3) return;
        StageBuffModel field = StageFieldAccess.Ensure(keyword);
        if (field == null)
            return;

        int amount = Math.Max(0, modular.GetNumFromParamString(args[2]));
        int before = StageFieldAccess.GetStack(keyword);
        if (string.Equals(operation, "add", StringComparison.OrdinalIgnoreCase))
        {
            field.AddStack(amount, modular.battleTiming, false);
        }
        else if (string.Equals(operation, "sub", StringComparison.OrdinalIgnoreCase))
        {
            if (keyword == BUFF_UNIQUE_KEYWORD.BloodDinner)
            {
                BattleUnitModel user = null;
                if (args.Length >= 4)
                {
                    var targets = modular.GetTargetModelList(args[3]);
                    if (targets != null)
                    {
                        var enumerator = targets.GetEnumerator();
                        if (enumerator.MoveNext())
                            user = enumerator.Current;
                    }
                }
                if (user == null)
                    user = StageFieldAccess.FindAnyGsoundUnit();

                int accumulatedBefore = BloodDinnerBuff.GetCommonAccumulativeUsedBloodDinner();
                if (user != null)
                    ((BloodDinnerBuff)field).UseBuffStack(user, amount, modular.battleTiming, null);
                else
                    field.SubStack(amount, modular.battleTiming, null);
                int accumulatedAfter = BloodDinnerBuff.GetCommonAccumulativeUsedBloodDinner();
                Plugin.Logger?.LogInfo(
                    "Gsound BloodDinner accumulated usage: " + accumulatedBefore +
                    " -> " + accumulatedAfter + " (requested " + amount + ")");
            }
            else
            {
                field.SubStack(amount, modular.battleTiming, null);
            }
        }
        else if (string.Equals(operation, "reset", StringComparison.OrdinalIgnoreCase))
        {
            field.ResetStack(modular.battleTiming);
        }
        int after = StageFieldAccess.GetStack(keyword);
        Plugin.Logger?.LogInfo(
            "Gsound StageField " + keyword + " " + operation + " " + amount +
            ": " + before + " -> " + after);
        StageFieldAccess.RefreshUi();
        if (keyword == BUFF_UNIQUE_KEYWORD.BloodDinner)
            StageFieldAccess.RefreshMapBloodVisual();
    }
 }

 internal sealed class StageFieldAcquirer : IModularAcquirer
 {
    public int ExecuteAcquirer(ModularSA modular, string functionName, string rawParams, string[] args)
    {
        if (args != null && args.Length >= 1 &&
            (string.Equals(args[0], "blooddinnerused", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(args[0], "commonusedblooddinner", StringComparison.OrdinalIgnoreCase)))
        {
            return BloodDinnerBuff.GetCommonAccumulativeUsedBloodDinner();
        }

        BUFF_UNIQUE_KEYWORD keyword;
        if (args == null || args.Length < 1 || !StageFieldAccess.TryResolve(args[0], out keyword))
            return 0;

        return StageFieldAccess.GetStack(keyword);
    }
 }

 internal sealed class PreyAuraConsequence : IModularConsequence
 {
    public void ExecuteConsequence(ModularSA modular, string functionName, string rawParams, string[] args)
    {
        if (args == null || args.Length < 2)
            return;
        bool active = modular.GetNumFromParamString(args[1]) > 0;
        var targets = modular.GetTargetModelList(args[0]);
        if (targets == null)
            return;
        var enumerator = targets.GetEnumerator();
        while (enumerator.MoveNext())
        {
            BattleUnitModel model = enumerator.Current;
            BattleUnitView view = BattleObjectManager.Instance.GetView(model);
            if (view == null)
                continue;
            // isCenter=false anchors to the view root instead of the head center;
            // scale=1 keeps the original size.
            Effect_Label eff = view.SetEffect_Label(
                "ThumbRoidionPreyAuraEffect", active, EFFECT_LAYER_TYPE.BACK,
                false, false, 1f, false);
            if (active && eff != null && eff.effectObj != null)
            {
                try
                {
                    // Anchor the ring under the doll (behind/under the character, on the
                    // ground) instead of floating at the head under the HP bar.
                    var t = eff.effectObj.transform;
                    if (view.ViewEffectRootBack != null)
                        t.SetParent(view.ViewEffectRootBack, false);
                    t.localPosition = new UnityEngine.Vector3(0f, -2f, 0f);
                    ChaosLineAccess.StripFront(eff.effectObj);
                }
                catch { }
            }
        }
    }
 }

 internal static class ChaosLineAccess
 {
    private static readonly HashSet<IntPtr> Stripped = new HashSet<IntPtr>();

    internal static void StripFrontOnView(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero)
            return;
        BattleUnitModel model = view.unitModel;
        if (!StageFieldAccess.IsGsoundUnit(model))
            return;
        if (!Stripped.Add(view.Pointer))
            return;
        try
        {
            if (view.Appearance != null)
                StripFront(view.Appearance.gameObject);
            if (view.ViewEffectRootBack != null)
                StripFront(view.ViewEffectRootBack.gameObject);
            if (view.gameObject != null)
                StripFront(view.gameObject);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Chaos line strip failed: " + ex.Message);
        }
    }

    internal static void StripFront(UnityEngine.GameObject root)
    {
        if (root == null)
            return;
        var candidates = new List<UnityEngine.Transform>();
        Collect(root.transform, candidates);
        if (candidates.Count == 0)
            return;
        UnityEngine.Transform front = candidates[0];
        float best = FrontScore(front);
        for (int i = 1; i < candidates.Count; i++)
        {
            float score = FrontScore(candidates[i]);
            if (score > best)
            {
                front = candidates[i];
                best = score;
            }
        }
        // Duplicate "(1)" / "2" copies are almost always the extra front stroke.
        for (int i = 0; i < candidates.Count; i++)
        {
            string name = candidates[i].name ?? "";
            if (name.IndexOf("(1)", StringComparison.Ordinal) >= 0
                || name.EndsWith(" 2", StringComparison.Ordinal)
                || name.EndsWith("2", StringComparison.Ordinal))
            {
                front = candidates[i];
                break;
            }
        }
        front.gameObject.SetActive(false);
        Plugin.Logger?.LogInfo(
            "Chaos line: disabled front '" + front.name +
            "' local=" + front.localPosition +
            " among " + candidates.Count);
    }

    private static float FrontScore(UnityEngine.Transform t)
    {
        UnityEngine.Vector3 local = t.localPosition;
        UnityEngine.Vector3 world = t.position;
        return local.x * 2f + local.z + world.z;
    }

    private static void Collect(UnityEngine.Transform node, List<UnityEngine.Transform> candidates)
    {
        if (node == null)
            return;
        if (node.gameObject.activeInHierarchy && IsChaosLine(node))
            candidates.Add(node);
        for (int i = 0; i < node.childCount; i++)
            Collect(node.GetChild(i), candidates);
    }

    private static bool IsChaosLine(UnityEngine.Transform node)
    {
        string n = (node.name ?? "").ToLowerInvariant();
        if (n.IndexOf("trail_b", StringComparison.Ordinal) >= 0
            || n.IndexOf("trail1", StringComparison.Ordinal) >= 0
            || n.IndexOf("trail2", StringComparison.Ordinal) >= 0
            || n.IndexOf("sword", StringComparison.Ordinal) >= 0
            || n.IndexOf("damage", StringComparison.Ordinal) >= 0
            || n.IndexOf("burn", StringComparison.Ordinal) >= 0)
            return false;
        bool named = n.IndexOf("line", StringComparison.Ordinal) >= 0
            || n.IndexOf("chaos", StringComparison.Ordinal) >= 0
            || n.IndexOf("confus", StringComparison.Ordinal) >= 0;
        if (!named)
            return false;
        return node.GetComponent<UnityEngine.ParticleSystem>() != null
            || node.GetComponent<UnityEngine.LineRenderer>() != null
            || node.GetComponent<UnityEngine.TrailRenderer>() != null;
    }

    internal static void Clear()
    {
        Stripped.Clear();
    }
 }

 internal sealed class RelocationFieldConsequence : IModularConsequence
 {
    public void ExecuteConsequence(ModularSA modular, string functionName, string rawParams, string[] args)
    {
        if (args == null || args.Length < 2)
            return;
        int mode = (int)modular.GetNumFromParamString(args[1]);
        var targets = modular.GetTargetModelList(args[0]);
        if (targets == null)
            return;
        var enumerator = targets.GetEnumerator();
        while (enumerator.MoveNext())
        {
            BattleUnitView view = BattleObjectManager.Instance.GetView(enumerator.Current);
            if (view == null)
                continue;
            if (mode >= 2)
                RelocationFieldAccess.Preload(view);
            else
                RelocationFieldAccess.Set(view, mode > 0);
        }
    }
 }

 internal static class RelocationFieldAccess
 {
    private const string DonorAppearanceId = "8504_kqe1j23Appearance";
    private static readonly Dictionary<IntPtr, UnityEngine.GameObject> Effects =
        new Dictionary<IntPtr, UnityEngine.GameObject>();
    private static readonly HashSet<IntPtr> Loading = new HashSet<IntPtr>();
    private static readonly HashSet<IntPtr> Requested = new HashSet<IntPtr>();
    private static readonly HashSet<IntPtr> Preloading = new HashSet<IntPtr>();
    private static readonly Dictionary<IntPtr, Il2CppSystem.Action<UnityEngine.GameObject>> Callbacks =
        new Dictionary<IntPtr, Il2CppSystem.Action<UnityEngine.GameObject>>();

    internal static void Set(BattleUnitView view, bool active)
    {
        if (view == null || view.Pointer == IntPtr.Zero)
            return;
        IntPtr key = view.Pointer;
        if (!active)
        {
            Requested.Remove(key);
            // The official Kqe S3 field runs longer than Gsound's borrowed S1
            // actor timeline.  It self-destructs after the original performance
            // window instead of being cut off by an early EndSkill callback.
            return;
        }

        Requested.Add(key);
        Preloading.Remove(key);
        UnityEngine.GameObject existing;
        if (Effects.TryGetValue(key, out existing) && existing != null)
        {
            Play(existing);
            return;
        }
        if (Loading.Contains(key))
            return;

        StartLoad(view, key);
    }

    internal static void Preload(BattleUnitView view)
    {
        if (view == null || view.Pointer == IntPtr.Zero)
            return;
        IntPtr key = view.Pointer;
        Requested.Add(key);
        UnityEngine.GameObject existing;
        if (Effects.TryGetValue(key, out existing) && existing != null)
            return;
        if (Loading.Contains(key))
            return;
        Preloading.Add(key);
        StartLoad(view, key);
    }

    private static void StartLoad(BattleUnitView view, IntPtr key)
    {
        try
        {
            Addressable.AddressableManager manager = Addressable.AddressableManager.Instance;
            if (manager == null || !manager.IsInitializedAddressable)
            {
                Plugin.Logger?.LogWarning("Relocation field: Addressables is not ready");
                return;
            }
            Addressable.KeyInfo keyInfo = Addressable.ResourceKeyBuilder.BuildSdResourceKeyInfo(
                Addressable.ResourceKeyBuilder.SdResourceType.Abnormality,
                DonorAppearanceId);
            Loading.Add(key);
            System.Action<UnityEngine.GameObject> managedCallback =
                donor => OnLoaded(key, view, donor);
            Il2CppSystem.Action<UnityEngine.GameObject> callback = managedCallback;
            Callbacks[key] = callback;
            manager.LoadAndInstantiateAsyncDev(
                keyInfo.label,
                keyInfo.resourceId,
                view.transform,
                callback);
            Plugin.Logger?.LogInfo("Relocation field: requested official " + DonorAppearanceId);
        }
        catch (Exception ex)
        {
            Loading.Remove(key);
            Callbacks.Remove(key);
            Plugin.Logger?.LogWarning("Relocation field request failed: " + ex);
        }
    }

    private static void OnLoaded(IntPtr key, BattleUnitView view, UnityEngine.GameObject donor)
    {
        Loading.Remove(key);
        Callbacks.Remove(key);
        bool preloadOnly = Preloading.Remove(key);
        if (donor == null)
        {
            Plugin.Logger?.LogWarning("Relocation field: official donor returned null");
            return;
        }
        try
        {
            if (view == null || view.Pointer == IntPtr.Zero || !Requested.Contains(key))
            {
                UnityEngine.Object.Destroy(donor);
                return;
            }
            donor.name = "Gsound_Relocation_Official_Kqe_Field";
            donor.transform.SetParent(view.transform, false);
            donor.transform.localPosition = UnityEngine.Vector3.zero;
            donor.transform.localRotation = UnityEngine.Quaternion.identity;
            donor.transform.localScale = UnityEngine.Vector3.one;
            Effects[key] = donor;
            if (preloadOnly)
            {
                donor.SetActive(false);
                Plugin.Logger?.LogInfo("Relocation field: official Kqe S3 donor preloaded");
            }
            else
            {
                Play(donor);
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Object.Destroy(donor);
            Plugin.Logger?.LogWarning("Relocation field setup failed: " + ex);
        }
    }

    private static void Play(UnityEngine.GameObject donor)
    {
        if (donor == null)
            return;
        donor.SetActive(true);

        // Do not call CharacterAppearance.ChangeMotion here. Lethe's
        // ChangeSkillMotion patch expects a real battle action owner and throws
        // for this detached visual donor, leaving Kqe's default body visible on
        // top of Gsound. Drive the original animation controllers directly and
        // expose only S3's field/effect hierarchy instead.
        int animatorCount = 0;
        UnityEngine.Animator rootAnimator = donor.GetComponent<UnityEngine.Animator>();
        if (PlayAnimatorState(rootAnimator, "S3"))
            animatorCount++;

        UnityEngine.Transform spine = FindDescendant(donor.transform, "[SpinePivot]");
        if (spine != null)
            spine.gameObject.SetActive(false);
        UnityEngine.Transform blood = FindDescendant(donor.transform, "[SpBlood]");
        if (blood != null)
            blood.gameObject.SetActive(false);

        UnityEngine.Transform spriteRoot = FindDescendant(donor.transform, "[SpRenderer]");
        if (spriteRoot != null)
        {
            UnityEngine.SpriteRenderer baseRenderer =
                spriteRoot.GetComponent<UnityEngine.SpriteRenderer>();
            if (baseRenderer != null)
                baseRenderer.enabled = false;
            UnityEngine.Transform motion = FindDescendant(spriteRoot, "Motion");
            if (motion != null)
            {
                for (int i = 0; i < motion.childCount; i++)
                {
                    UnityEngine.Transform child = motion.GetChild(i);
                    child.gameObject.SetActive(
                        string.Equals(child.name, "skill3", StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        UnityEngine.Transform machine = FindDescendant(donor.transform, "Machine");
        if (machine != null)
        {
            machine.gameObject.SetActive(true);
            if (PlayAnimatorState(machine.GetComponent<UnityEngine.Animator>(), "S3_Machine"))
                animatorCount++;
        }
        UnityEngine.Transform machineSprite = FindDescendant(donor.transform, "Machine_Sprite");
        if (machineSprite != null)
            machineSprite.gameObject.SetActive(true);

        animatorCount += PlayOriginalEffect(donor.transform, "FX_Mon_Train1_Robot_SKL2",
            "FX_Mon_Train1_Robot_SKL2_ani");
        animatorCount += PlayOriginalEffect(donor.transform, "FX_Mon_Train1_Robot_Arm",
            "FX_Mon_Train1_Robot_Arm_ani");
        animatorCount += PlayOriginalEffect(donor.transform, "FX_Mon_Train1_Robot_ArmPiercing",
            "FX_Mon_Train1_Robot_ArmPiercing_ani");

        UnityEngine.Object.Destroy(donor, 4.5f);
        Plugin.Logger?.LogInfo(
            "Relocation field: playing isolated official Kqe S3 layers, animators=" + animatorCount);
    }

    private static UnityEngine.Transform FindDescendant(
        UnityEngine.Transform root,
        string exactName)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, exactName, StringComparison.Ordinal))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            UnityEngine.Transform found = FindDescendant(root.GetChild(i), exactName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static bool PlayAnimatorState(UnityEngine.Animator animator, string stateName)
    {
        if (animator == null)
            return false;
        try
        {
            animator.gameObject.SetActive(true);
            animator.enabled = true;
            animator.Rebind();
            int fullHash = UnityEngine.Animator.StringToHash("Base Layer." + stateName);
            int shortHash = UnityEngine.Animator.StringToHash(stateName);
            if (animator.HasState(0, fullHash))
                animator.Play(fullHash, 0, 0f);
            else if (animator.HasState(0, shortHash))
                animator.Play(shortHash, 0, 0f);
            else
                return false;
            animator.Update(0f);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning(
                "Relocation field animator failed state=" + stateName + ": " + ex.Message);
            return false;
        }
    }

    private static int PlayOriginalEffect(
        UnityEngine.Transform donorRoot,
        string effectRootName,
        string animatorState)
    {
        UnityEngine.Transform effectRoot = FindDescendant(donorRoot, effectRootName);
        if (effectRoot == null)
            return 0;
        effectRoot.gameObject.SetActive(true);
        int played = 0;
        UnityEngine.Animator[] animators =
            effectRoot.GetComponentsInChildren<UnityEngine.Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (PlayAnimatorState(animators[i], animatorState))
                played++;
        }
        PlayParticles(effectRoot.gameObject);
        return played;
    }

    private static void PlayParticles(UnityEngine.GameObject root)
    {
        if (root == null)
            return;
        UnityEngine.ParticleSystem[] particles =
            root.GetComponentsInChildren<UnityEngine.ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;
            particles[i].gameObject.SetActive(true);
            particles[i].Play(true);
        }
    }

    internal static void Clear()
    {
        foreach (UnityEngine.GameObject effect in Effects.Values)
        {
            if (effect != null)
                UnityEngine.Object.Destroy(effect);
        }
        Effects.Clear();
        Loading.Clear();
        Requested.Clear();
        Preloading.Clear();
        Callbacks.Clear();
    }
 }

 internal sealed class BlackFogConsequence : IModularConsequence
 {
    public void ExecuteConsequence(ModularSA modular, string functionName, string rawParams, string[] args)
    {
        if (args == null || args.Length < 2)
            return;
        bool active = modular.GetNumFromParamString(args[1]) > 0;
        var targets = modular.GetTargetModelList(args[0]);
        if (targets == null)
            return;
        var enumerator = targets.GetEnumerator();
        while (enumerator.MoveNext())
        {
            BattleUnitView view = BattleObjectManager.Instance.GetView(enumerator.Current);
            if (view == null)
                continue;
            try
            {
                BlackFogAccess.Set(view, active);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning("Gsound black fog failed: " + ex.Message);
            }
        }
    }
 }

 internal static class BlackFogAccess
 {
    private static readonly Addressable.ResourceKeyBuilder.SdResourceType[] DonorResourceTypes =
    {
        Addressable.ResourceKeyBuilder.SdResourceType.Abnormality
    };

    private static readonly string[] DonorAppearanceIds =
    {
        "8153_KimSatGat_ErodeAppearance"
    };

    private static readonly Dictionary<IntPtr, List<UnityEngine.GameObject>> Effects =
        new Dictionary<IntPtr, List<UnityEngine.GameObject>>();
    private static readonly HashSet<IntPtr> Loading = new HashSet<IntPtr>();
    private static readonly Dictionary<IntPtr, Il2CppSystem.Action<UnityEngine.GameObject>> Callbacks =
        new Dictionary<IntPtr, Il2CppSystem.Action<UnityEngine.GameObject>>();

    internal static void Set(BattleUnitView view, bool active)
    {
        if (view == null || view.Pointer == IntPtr.Zero)
            return;
        IntPtr key = view.Pointer;
        try
        {
            // Official T Corp "Borrowed Time" overlay. Keep this independent
            // from the extracted Kim effect so one can still render if the
            // other asset is unavailable on the current map.
            view.SetEffect_Ability(
                BUFF_UNIQUE_KEYWORD.TimeRentalTwoPersonality,
                active,
                EFFECT_LAYER_TYPE.BACK,
                false,
                1f);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning("Black fog: Borrowed Time overlay failed: " + ex.Message);
        }
        List<UnityEngine.GameObject> existing;
        if (Effects.TryGetValue(key, out existing) && existing != null && existing.Count > 0)
        {
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i] == null)
                    continue;
                existing[i].SetActive(active);
                if (active)
                    PlayParticles(existing[i]);
            }
            return;
        }
        if (!active)
            return;
        if (Loading.Contains(key))
            return;
        TryLoadDonor(view, key, 0);
    }

    private static void TryLoadDonor(BattleUnitView view, IntPtr key, int donorIndex)
    {
        if (donorIndex >= DonorAppearanceIds.Length)
        {
            Plugin.Logger?.LogWarning("Black fog: Distorted Bamboo-hatted Kim yielded no usable back FX; Borrowed Time remains active");
            return;
        }
        try
        {
            Addressable.AddressableManager manager = Addressable.AddressableManager.Instance;
            if (manager == null || !manager.IsInitializedAddressable)
            {
                Plugin.Logger?.LogWarning("Black fog: Addressables is not ready");
                return;
            }
            Addressable.KeyInfo keyInfo = Addressable.ResourceKeyBuilder.BuildSdResourceKeyInfo(
                DonorResourceTypes[donorIndex],
                DonorAppearanceIds[donorIndex]);
            Loading.Add(key);
            int capturedIndex = donorIndex;
            System.Action<UnityEngine.GameObject> managedCallback =
                donor => OnDonorLoaded(key, view, donor, capturedIndex);
            Il2CppSystem.Action<UnityEngine.GameObject> callback = managedCallback;
            Callbacks[key] = callback;
            manager.LoadAndInstantiateAsyncDev(
                keyInfo.label,
                keyInfo.resourceId,
                view.transform,
                callback);
            Plugin.Logger?.LogInfo("Black fog: requested " + DonorAppearanceIds[donorIndex]);
        }
        catch (Exception ex)
        {
            Loading.Remove(key);
            Callbacks.Remove(key);
            Plugin.Logger?.LogWarning("Black fog: Addressables request failed: " + ex.Message);
        }
    }

    private static void OnDonorLoaded(
        IntPtr key,
        BattleUnitView view,
        UnityEngine.GameObject donor,
        int donorIndex)
    {
        Loading.Remove(key);
        Callbacks.Remove(key);
        if (donor == null)
        {
            TryLoadDonor(view, key, donorIndex + 1);
            return;
        }
        try
        {
            donor.SetActive(false);
            if (view == null || view.Pointer == IntPtr.Zero)
            {
                UnityEngine.Object.Destroy(donor);
                return;
            }

            LogDonorTree(donor.transform, 0);
            UnityEngine.Transform fx = FindBestFxRoot(donor.transform);
            if (fx == null)
            {
                UnityEngine.Object.Destroy(donor);
                TryLoadDonor(view, key, donorIndex + 1);
                return;
            }

            UnityEngine.Transform parent = view.ViewEffectRootBack;
            if (parent == null)
                parent = view.transform;
            fx.SetParent(parent, false);
            fx.gameObject.name = "Gsound_WhitePaper_RienBlackFog";
            fx.localPosition = new UnityEngine.Vector3(0f, -0.4f, 0f);
            fx.localRotation = UnityEngine.Quaternion.identity;
            fx.localScale = UnityEngine.Vector3.one;
            fx.gameObject.SetActive(true);
            PlayParticles(fx.gameObject);
            List<UnityEngine.GameObject> bucket;
            if (!Effects.TryGetValue(key, out bucket) || bucket == null)
            {
                bucket = new List<UnityEngine.GameObject>();
                Effects[key] = bucket;
            }
            bucket.Add(fx.gameObject);
            UnityEngine.Object.Destroy(donor);
            Plugin.Logger?.LogInfo(
                "Black fog: extracted '" + fx.name + "' from " + DonorAppearanceIds[donorIndex]);
            TryLoadDonor(view, key, donorIndex + 1);
        }
        catch (Exception ex)
        {
            UnityEngine.Object.Destroy(donor);
            Plugin.Logger?.LogWarning("Black fog: donor extraction failed: " + ex.Message);
        }
    }

    private static UnityEngine.Transform FindBestFxRoot(UnityEngine.Transform root)
    {
        UnityEngine.Transform best = null;
        int bestScore = int.MinValue;
        UnityEngine.Transform[] all = root.GetComponentsInChildren<UnityEngine.Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            UnityEngine.Transform node = all[i];
            if (node == null || node == root)
                continue;
            int score = ScoreFx(node);
            if (score > bestScore)
            {
                bestScore = score;
                best = node;
            }
        }
        if (best == null || bestScore < 10)
        {
            Plugin.Logger?.LogWarning("Black fog: no scored FX root (best=" + bestScore + ")");
            return null;
        }
        UnityEngine.Transform walk = best.parent;
        while (walk != null && walk != root && ScoreFx(walk) >= bestScore - 15)
        {
            best = walk;
            walk = walk.parent;
        }
        return best;
    }

    private static int ScoreFx(UnityEngine.Transform node)
    {
        string n = (node.name ?? "").ToLowerInvariant();
        if (n.IndexOf("appearance", StringComparison.Ordinal) >= 0
            || n.IndexOf("spine", StringComparison.Ordinal) >= 0
            || n.IndexOf("mesh", StringComparison.Ordinal) >= 0
            || n.IndexOf("dummy", StringComparison.Ordinal) >= 0)
            return -100;

        int score = 0;
        if (n.IndexOf("nightmare", StringComparison.Ordinal) >= 0) score += 100;
        if (n.IndexOf("distort", StringComparison.Ordinal) >= 0) score += 120;
        if (n.IndexOf("erode", StringComparison.Ordinal) >= 0) score += 100;
        if (n.IndexOf("kimsatgat", StringComparison.Ordinal) >= 0) score += 80;
        if (n.IndexOf("noise", StringComparison.Ordinal) >= 0) score += 90;
        if (n.IndexOf("rental", StringComparison.Ordinal) >= 0) score += 80;
        if (n.IndexOf("tcorp", StringComparison.Ordinal) >= 0) score += 80;
        if (n.IndexOf("black", StringComparison.Ordinal) >= 0) score += 40;
        if (n.IndexOf("buff", StringComparison.Ordinal) >= 0) score += 35;
        if (n.IndexOf("aura", StringComparison.Ordinal) >= 0) score += 30;
        if (n.IndexOf("fog", StringComparison.Ordinal) >= 0) score += 30;
        if (n.IndexOf("smoke", StringComparison.Ordinal) >= 0) score += 25;
        if (n.IndexOf("shadow", StringComparison.Ordinal) >= 0) score += 20;
        if (n.StartsWith("fx_", StringComparison.Ordinal) || n.StartsWith("fx ", StringComparison.Ordinal))
            score += 15;

        UnityEngine.ParticleSystem[] particles =
            node.GetComponentsInChildren<UnityEngine.ParticleSystem>(true);
        if (particles != null)
            score += particles.Length * 3;
        return score;
    }

    private static void LogDonorTree(UnityEngine.Transform node, int depth)
    {
        if (node == null || depth > 3)
            return;
        UnityEngine.ParticleSystem[] ps = node.GetComponents<UnityEngine.ParticleSystem>();
        Plugin.Logger?.LogInfo(
            "Black fog tree " + new string('.', depth) + node.name +
            " ps=" + (ps == null ? 0 : ps.Length));
        for (int i = 0; i < node.childCount && i < 24; i++)
            LogDonorTree(node.GetChild(i), depth + 1);
    }

    private static void PlayParticles(UnityEngine.GameObject root)
    {
        if (root == null)
            return;
        UnityEngine.ParticleSystem[] particles = root.GetComponentsInChildren<UnityEngine.ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;
            particles[i].gameObject.SetActive(true);
            particles[i].Play(true);
        }
    }

    internal static void Clear()
    {
        foreach (List<UnityEngine.GameObject> bucket in Effects.Values)
        {
            if (bucket == null)
                continue;
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i] != null)
                    UnityEngine.Object.Destroy(bucket[i]);
            }
        }
        Effects.Clear();
        Loading.Clear();
        Callbacks.Clear();
    }
 }

}
