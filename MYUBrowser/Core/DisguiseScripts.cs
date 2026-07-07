namespace MYUBrowser.Core;

/// <summary>注入网页的伪装/控制脚本与样式</summary>
public static class DisguiseScripts
{
    /// <summary>暂停页面内所有 video/audio，并递归处理同源 iframe</summary>
    public const string PauseAllMedia = """
        (function () {
            function pause(doc) {
                try {
                    doc.querySelectorAll('video,audio').forEach(function (m) { try { m.pause(); } catch (e) { } });
                    doc.querySelectorAll('iframe').forEach(function (f) {
                        try { if (f.contentDocument) pause(f.contentDocument); } catch (e) { }
                    });
                } catch (e) { }
            }
            pause(document);
        })();
        """;

    /// <summary>深色滤镜：整页反色变暗，图片/视频再反转回来保持正常观感</summary>
    public const string DarkFilterCss = """
        html { filter: invert(0.92) hue-rotate(180deg) !important; background: #101010 !important; }
        img, video, picture, canvas, iframe, embed, object, svg image {
            filter: invert(1) hue-rotate(180deg) !important;
        }
        """;

    /// <summary>低调模式：图片/视频灰度化并大幅淡化，页面看起来像纯文字资料</summary>
    public const string HideImagesCss = """
        img, video, picture, svg, canvas, embed, object {
            opacity: 0.07 !important;
            filter: grayscale(1) !important;
            transition: opacity .15s;
        }
        img:hover, video:hover, picture:hover, canvas:hover { opacity: 1 !important; filter: none !important; }
        """;

    /// <summary>按 id 注入或移除一段 style，enable 为 js 布尔字面量</summary>
    public static string ToggleStyle(string id, string css, bool enable)
    {
        var cssJson = System.Text.Json.JsonSerializer.Serialize(css);
        var idJson = System.Text.Json.JsonSerializer.Serialize(id);
        var enableJs = enable ? "true" : "false";
        return $$"""
            (function () {
                var id = {{idJson}};
                var old = document.getElementById(id);
                if ({{enableJs}}) {
                    if (!old) {
                        var s = document.createElement('style');
                        s.id = id;
                        s.textContent = {{cssJson}};
                        (document.head || document.documentElement).appendChild(s);
                    }
                } else if (old) {
                    old.remove();
                }
            })();
            """;
    }

    /// <summary>随文档创建注入的快捷键监听：Ctrl+↑/↓ 调透明度、Ctrl+M 极简模式</summary>
    public const string HotkeyListener = """
        window.addEventListener('keydown', function (e) {
            if (!e.ctrlKey || e.shiftKey || e.altKey || e.metaKey) return;
            var cmd = null;
            if (e.key === 'ArrowUp') cmd = 'opacity-up';
            else if (e.key === 'ArrowDown') cmd = 'opacity-down';
            else if (e.key === 'm' || e.key === 'M') cmd = 'minimal';
            if (cmd) {
                e.preventDefault();
                e.stopPropagation();
                try { window.chrome.webview.postMessage('myu:' + cmd); } catch (err) { }
            }
        }, true);
        """;
}
