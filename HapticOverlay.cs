using UnityEngine;

namespace LovenseRoR2;

internal static class HapticOverlay
{
    private static Texture2D _bgTex    = null!;
    private static Texture2D _barBgTex = null!;
    private static Texture2D _fillTex  = null!;
    private static GUIStyle  _titleStyle = null!;
    private static GUIStyle  _pctStyle   = null!;
    private static bool _stylesReady;

    private const int PanelW  = 220;
    private const int PanelH  = 62;
    private const int Padding = 8;
    private const int BarH    = 18;

    internal static void Initialize()
    {
        _bgTex    = MakeTex(new Color(0f,    0f,    0f,    0.75f));
        _barBgTex = MakeTex(new Color(0.15f, 0.15f, 0.15f, 0.9f));
        _fillTex  = MakeTex(Color.white); // tinted per-draw via GUI.color
    }

    internal static void Draw()
    {
        if (!PluginConfig.ShowOverlay.Value) return;

        // GUIStyles must be created after Unity skin is ready (not in Initialize)
        if (!_stylesReady)
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = Color.white },
            };
            _pctStyle = new GUIStyle(_titleStyle)
            {
                fontSize  = 18,
                alignment = TextAnchor.MiddleRight,
            };
            _stylesReady = true;
        }

        int x = PluginConfig.OverlayX.Value;
        int y = PluginConfig.OverlayY.Value;

        // background panel
        GUI.DrawTexture(new Rect(x, y, PanelW, PanelH), _bgTex);

        // "LOVENSE" title — left side
        GUI.Label(new Rect(x + Padding, y + Padding, PanelW / 2f, 24), "LOVENSE", _titleStyle);

        // intensity % — right side; "—" when disconnected
        string label = LovensePlugin.ToyId != null ? $"{LovensePlugin.CurrentPercent}%" : "—";
        GUI.Label(new Rect(x + Padding, y + Padding, PanelW - Padding * 2, 24), label, _pctStyle);

        // progress bar
        int barX = x + Padding;
        int barY = y + Padding + 26;
        int barW = PanelW - Padding * 2;

        GUI.DrawTexture(new Rect(barX, barY, barW, BarH), _barBgTex);

        float pct = LovensePlugin.CurrentPercent / 100f;
        if (pct > 0f)
        {
            // soft pink at low intensity → hot magenta at full
            GUI.color = Color.Lerp(new Color(1f, 0.55f, 0.78f), new Color(1f, 0.08f, 0.42f), pct);
            GUI.DrawTexture(new Rect(barX, barY, barW * pct, BarH), _fillTex);
            GUI.color = Color.white;
        }
    }

    private static Texture2D MakeTex(Color color)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, color);
        t.Apply();
        return t;
    }
}
