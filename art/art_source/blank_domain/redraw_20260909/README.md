# 2026-09-09 大剑重绘原稿

用户已否决普通窄短剑。武器按 greatsword-reference.png 的超长咒灵大剑设计，人物约四头身，动作优先参考原版狂猎希斯的大剑帧。

三张动作原稿：windup-whunt-green.png（蓄力）、slash-whunt-green.png（横斩）、followthrough-whunt-green.png（收势）。另从已有透明大剑稿补出 skill3 末段 thrust（前突）帧。使用内置 image_gen；未指定或确认具体模型。完整提示词见 prompt-set.json。

2026-09-09 21:55：用户已明确授权 Python 去底、切帧、统一比例并完成入包。1.11.15 已安装，processed 目录内是最终透明 PNG、脚底锚点清单、原版对照图以及原版换帧时间表；`_codex_work/gsound-1.11.15/art/thrust.png` 与运行时 RGBA 是新增的三技能末段突刺帧。游戏内视觉仍待验证。

待机最终稿是 idle-ready-green.png，双手握柄、剑指左上，替换旧待机。idle-master.png 的窄剑已否决，idle-cursed-master-green.png 也未采用。所有处理按头部尺寸统一到 112 像素，以各帧脚底中点为锚，PPU 100；不按含剑包围盒缩放。整套当前使用待机、蓄力、横斩、收势四个主姿势，覆盖 57 个 Sprite 引用；没有声称重绘了原版所有独立动作。

内置生图提示词：prompt-set.json（三张动作稿）与 idle-prompt.json（最终待机）。构建与校验脚本在 D:\limbus\_codex_work\gsound-1.11.14：prepare_redraw.py、build_redraw_bundles.py、extract_frame_schedule.py、validate_release.py。新版新增独立 Texture2D，并恢复旧脚本覆盖的原版共享图集，原版 FX 贴图像素已逐项核对。
