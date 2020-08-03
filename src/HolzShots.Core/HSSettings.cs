using System;
using HolzShots;
using HolzShots.IO;
using HolzShots.Input;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace HolzShots
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:Uri properties should not be strings")]
    public class HSSettings
    {
        [JsonProperty("$schema")]
        public string SchemaUrl { get; } = "";
        public string Version { get; } = "0.1.0";

        #region save.*

        [JsonProperty("save.enable")]
        public bool SaveImagesToLocalDisk { get; private set; } = true;
        [JsonProperty("save.path")]
        public string SavePath { get; private set; } = HolzShotsPaths.DefaultScreenshotSavePath;
        [JsonProperty("save.pattern")]
        public string SaveFileNamePattern { get; private set; } = "Screenshot-<Date>";

        #endregion

        [JsonProperty("editor.closeAfterUpload")]
        public bool CloseAfterUpload { get; private set; } = false;
        /// <summary> TODO: Use this property </summary>
        [JsonProperty("editor.closeAfterSave")]
        public bool CloseAfterSave { get; private set; } = false;

        public bool EnableLinkViewer { get; private set; } = true;
        /// <summary> Needs <see cref="EnableLinkViewer"/> to be set to true. Will do nothing otherwise. </summary>
        public bool AutoCloseLinkViewer { get; private set; } = true;
        public bool EnableUploadProgressToast { get; private set; } = true;
        public bool ShowCopyConfirmation { get; private set; } = false;

        /// <summary>
        /// If disabled, it does not show the Shot Editor but uploads it instead.
        /// We may just add a parameter to the key bindings to be able to configure this on a key-binding basis.
        /// 
        /// TODO: Find better name.
        /// </summary>
        public bool EnableShotEditor { get; private set; } = true;

        /// <summary> TODO: Change name </summary>
        public bool EnableIngameMode { get; private set; } = false;
        /// <summary> TODO: Maybe use a different name for that. </summary>
        public bool EnableSmartFormatForUpload { get; private set; } = false;
        public bool EnableSmartFormatForSaving { get; private set; } = false;

        public string TrayIconDoubleClickCommand { get; set; } = null;

        // TODO: Fix visibility
        [JsonProperty("keyBindings")]
        public IReadOnlyList<KeyBinding> KeyBindings { get; set; } = ImmutableList<KeyBinding>.Empty;
    }
    public class KeyBinding
    {
        // TODO: Fix visibility
        public bool Enabled { get; set; } = false;
        public string Command { get; set; } = null;
        public Hotkey Keys { get; set; } = null;
    }
}
