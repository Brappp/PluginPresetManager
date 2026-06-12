using System.Numerics;

namespace PluginPresetManager.UI;

public static class Colors
{
    public static readonly Vector4 Primary = new(0.4f, 0.7f, 1f, 1f);
    public static readonly Vector4 PrimaryHover = new(0.5f, 0.8f, 1f, 1f);

    public static readonly Vector4 Success = new(0.4f, 1f, 0.6f, 1f);
    public static readonly Vector4 Warning = new(1f, 0.8f, 0.4f, 1f);
    public static readonly Vector4 Error = new(1f, 0.4f, 0.4f, 1f);

    public static readonly Vector4 TextNormal = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 TextMuted = new(0.5f, 0.5f, 0.5f, 1f);
    public static readonly Vector4 TextDisabled = new(0.4f, 0.4f, 0.4f, 1f);

    public static readonly Vector4 Header = new(0.7f, 0.9f, 1f, 1f);

    public static readonly Vector4 Star = new(1f, 0.85f, 0.3f, 1f);
    public static readonly Vector4 Active = Success;
    public static readonly Vector4 Inactive = new(0.6f, 0.6f, 0.6f, 1f);

    public static readonly Vector4 TagDev = new(1f, 0.4f, 1f, 1f);
    public static readonly Vector4 TagThirdParty = new(1f, 1f, 0.4f, 1f);

    public static readonly Vector4 ButtonPrimary = new(0.12f, 0.31f, 0.47f, 1f);
    public static readonly Vector4 ButtonPrimaryHover = new(0.15f, 0.37f, 0.58f, 1f);
    public static readonly Vector4 ButtonDefault = new(0.3f, 0.3f, 0.1f, 1f);
    public static readonly Vector4 LoadedDot = new(0.4f, 1f, 0.6f, 0.9f);
    public static readonly Vector4 UnloadedDot = new(0.45f, 0.45f, 0.45f, 0.9f);
}

public static class Sizing
{
    public const float ButtonSmall = 60f;
    public const float ButtonMedium = 80f;
    public const float ButtonLarge = 100f;
    public const float ButtonWide = 120f;

    public const float SpacingSmall = 4f;
    public const float SpacingMedium = 8f;
    public const float SpacingLarge = 16f;

    public const float InputSmall = 100f;
    public const float InputMedium = 150f;
    public const float InputLarge = 200f;

    public const float LeftPanelWidth = 180f;
}
