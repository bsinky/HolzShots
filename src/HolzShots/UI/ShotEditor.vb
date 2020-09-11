Imports System.Drawing.Drawing2D
Imports System.Drawing.Printing
Imports System.IO
Imports System.Runtime.InteropServices
Imports HolzShots.Interop
Imports HolzShots.Net
Imports HolzShots.UI.Controls
Imports HolzShots.UI.Controls.Helpers
Imports Microsoft.WindowsAPICodePack.Taskbar
Imports HolzShots.Composition
Imports HolzShots.Drawing

Namespace UI.Specialized
    Friend Class ShotEditor
        Inherits System.Windows.Forms.Form
        Implements IDisposable

        Private _activator As PanelActivator

#Region "Properties"

        ' TODO: Remove?
        Friend ReadOnly Property CurrentTool As PaintPanel.ShotEditorTool
            Get
                'Return ThePanel.CurrentTool
            End Get
        End Property

        Friend Property Screenshot As Screenshot

        Private ReadOnly _imageHoster As UploaderEntry ' ?
        Private ReadOnly _settingsContext As HSSettings

#End Region

#Region "Win7/8-Thumbnails"

        Private _uploadThumbnailButton As ThumbnailToolBarButton
        Private _saveThumbnailButton As ThumbnailToolBarButton
        Private _copyThumbnailButton As ThumbnailToolBarButton

        Private Sub InitializeThumbnailToolbar()

            If TaskbarManager.IsPlatformSupported Then

                Dim uploadTooltip As String = String.Empty
                If _imageHoster?.Metadata IsNot Nothing Then
                    uploadTooltip = UploadToHoster.ToolTipText.Remove(UploadToHoster.ToolTipText.IndexOf(" (", StringComparison.Ordinal))
                    uploadTooltip = String.Format(Global.HolzShots.My.Application.TheCulture, uploadTooltip, _imageHoster.Metadata.Name)
                End If

                _uploadThumbnailButton = New ThumbnailToolBarButton(Icon.FromHandle(HolzShots.My.Resources.uploadMedium.GetHicon()), uploadTooltip)
                AddHandler _uploadThumbnailButton.Click, Sub() UploadCurrentImageToDefaultProvider()
                _uploadThumbnailButton.Enabled = _imageHoster?.Metadata IsNot Nothing

                _saveThumbnailButton = New ThumbnailToolBarButton(Icon.FromHandle(HolzShots.My.Resources.saveMedium.GetHicon()), "Save image")
                _copyThumbnailButton = New ThumbnailToolBarButton(Icon.FromHandle(HolzShots.My.Resources.clipboardMedium.GetHicon()), "Copy image")

                AddHandler _saveThumbnailButton.Click, Sub() SaveImage()
                AddHandler _copyThumbnailButton.Click, Sub() CopyImage()

                TaskbarManager.Instance.ThumbnailToolBars.AddButtons(Handle, _uploadThumbnailButton, _saveThumbnailButton, _copyThumbnailButton)
            End If
        End Sub

#End Region

#Region "Ctors"

        Public Sub New(ByVal screenshot As Screenshot, settingsContext As HSSettings)
            Debug.Assert(screenshot IsNot Nothing)
            Debug.Assert(screenshot.Image IsNot Nothing)
            Debug.Assert(settingsContext IsNot Nothing)

            _settingsContext = settingsContext

            InitializeComponent()

            autoCloseShotEditor.Checked = settingsContext.CloseAfterUpload
            autoCloseShotEditor.Enabled = False ' We only support reading that setting for now

            If settingsContext.ShotEditorTitle IsNot Nothing Then
                Text = settingsContext.ShotEditorTitle
            End If

            Me.Screenshot = screenshot

            _imageHoster = UserSettings.GetImageServiceForSettingsContext(settingsContext, HolzShots.My.Application.Uploaders)

            InitializeThumbnailToolbar()

            If Not settingsContext.EnableHotkeysDuringFullscreen AndAlso HolzShots.Windows.Forms.EnvironmentEx.IsFullscreenAppRunning() Then
                WindowState = FormWindowState.Minimized
            ElseIf screenshot.Size = SystemInformation.VirtualScreen.Size Then
                WindowState = FormWindowState.Maximized
            ElseIf screenshot.Image.ShouldMaximizeEditorWindowForImage() Then
                WindowState = FormWindowState.Maximized
            Else
                Width = screenshot.Image.Width
                Height = screenshot.Image.Height + ThePanel.Location.Y + 140
                WindowState = FormWindowState.Normal
            End If

            AddSettingsPanels()

            CensorSettingsPanel.BackColor = Color.Transparent
            MarkerSettingsPanel.BackColor = Color.Transparent
            EraserSettingsPanel.BackColor = Color.Transparent
            EllipseSettingsPanel.BackColor = Color.Transparent
            BrightenSettingsPanel.BackColor = Color.Transparent
            ArrowSettingsPanel.BackColor = Color.Transparent
            BlurSettingsPanel.BackColor = Color.Transparent

            Dim focusColor As Color = BackColor

            BlurnessBar.BackColor = focusColor
            ZensursulaBar.BackColor = focusColor
            MarkerBar.BackColor = focusColor
            BlackWhiteTracker.BackColor = focusColor
            ArrowWidthSlider.BackColor = focusColor

            EllipseBar.BackColor = focusColor
            EllipseOrRectangle.BackColor = focusColor

            EraserBar.BackColor = focusColor

            ShareStrip.BackColor = Color.Transparent
            EditStrip.BackColor = Color.Transparent
            ToolStrip1.BackColor = Color.Transparent
            CopyPrintToolStrip.BackColor = Color.Transparent

            DrawCursor.Visible = screenshot.Source <> ScreenshotSource.Selected AndAlso screenshot.Source <> ScreenshotSource.Unknown


            UploadToHoster.Enabled = _imageHoster?.Metadata IsNot Nothing
            UploadToHoster.ToolTipText = If(
                            _imageHoster?.Metadata IsNot Nothing,
                            String.Format(Global.HolzShots.My.Application.TheCulture, UploadToHoster.ToolTipText, _imageHoster?.Metadata.Name),
                            String.Empty
                        )

            Dim renderer = HolzShots.Windows.Forms.EnvironmentEx.GetToolStripRendererForCurrentTheme()
            ShareStrip.Renderer = renderer
            ToolStrip1.Renderer = renderer
            EditStrip.Renderer = renderer
            CopyPrintToolStrip.Renderer = renderer
        End Sub

#End Region

#Region "Form Events"

        Private Sub ShotShowerFormClosing(ByVal sender As Object, ByVal e As FormClosingEventArgs) Handles MyBase.FormClosing
            UpdateSettings()
        End Sub

        Private Sub ShotShowerLoad(ByVal sender As Object, ByVal e As EventArgs) Handles MyBase.Load
            ThePanel.Initialize(Screenshot)
            LoadToolSettings()
        End Sub

#Region "Settings and stuff"

        Private Sub LoadToolSettings()
            LoadZensursula()
            LoadArrow()
            LoadMarker()
            LoadEraser()
            LoadEllipse()
            LoadBrighten()
            LoadPixelator()
            EnlistUploaderPlugins()
        End Sub

        Private Sub EnlistUploaderPlugins()

            UploadToHoster.DropDown.ImageScalingSize = New Size(16, 16)
            UploadToHoster.DropDown.AutoSize = True
            If HolzShots.My.Application.Uploaders.Loaded Then
                UploadToHoster.DropDown.Renderer = HolzShots.Windows.Forms.EnvironmentEx.GetToolStripRendererForCurrentTheme()
                Dim pls = HolzShots.My.Application.Uploaders.GetUploaderNames()
                For Each uploaderName In pls
                    Dim item As ToolStripItem = UploadToHoster.DropDown.Items.Add(String.Format(Localization.UploadTo, uploaderName))
                    item.Tag = uploaderName
                    item.ImageScaling = ToolStripItemImageScaling.None
                Next
            End If
        End Sub

        Private Sub LoadPixelator()
            If HolzShots.My.Settings.BlurFactor > 30 OrElse HolzShots.My.Settings.BlurFactor <= 5 Then
                HolzShots.My.Settings.BlurFactor = 7
                HolzShots.My.Settings.Save()
            End If
            ThePanel.BlurFactor = HolzShots.My.Settings.BlurFactor
            BlurnessBar.Value = ThePanel.BlurFactor
        End Sub

        Private Sub LoadZensursula()
            Zensursula_Viewer.Color = HolzShots.My.Settings.ZensursulaColor
            If HolzShots.My.Settings.ZensursulaWidth > 100 OrElse HolzShots.My.Settings.ZensursulaWidth <= 0 Then
                HolzShots.My.Settings.ZensursulaWidth = 20
                HolzShots.My.Settings.Save()
            End If
            ZensursulaBar.Value = HolzShots.My.Settings.ZensursulaWidth
            Pinsel_Width_Zensursula.Text = $"{ZensursulaBar.Value}px"
            ThePanel.ZensursulaColor = Zensursula_Viewer.Color
            ThePanel.ZensursulaWidth = ZensursulaBar.Value
        End Sub

        Private Sub LoadArrow()
            ArrowColorviewer.Color = HolzShots.My.Settings.ArrowColor
            ThePanel.ArrowColor = ArrowColorviewer.Color
            ArrowWidthSlider.Value = If(HolzShots.My.Settings.ArrowWidth <= ArrowWidthSlider.Maximum,
                                        If(HolzShots.My.Settings.ArrowWidth >= ArrowWidthSlider.Minimum, HolzShots.My.Settings.ArrowWidth, 0), 0)
            ArrowWidthSliderScroll(Nothing, Nothing)
        End Sub

        Private Sub LoadMarker()
            Marker_Viewer.Color = HolzShots.My.Settings.MarkerColor
            If HolzShots.My.Settings.MarkerWidth > 100 OrElse HolzShots.My.Settings.MarkerWidth <= 0 Then
                HolzShots.My.Settings.MarkerWidth = 20
                HolzShots.My.Settings.Save()
            End If
            MarkerBar.Value = HolzShots.My.Settings.MarkerWidth
            Pinsel_Width_Marker.Text = $"{MarkerBar.Value}px"
            ThePanel.MarkerColor = Marker_Viewer.Color
            ThePanel.MarkerWidth = MarkerBar.Value
        End Sub

        Private Sub LoadEllipse()
            Ellipse_Viewer.Color = HolzShots.My.Settings.EllipseColor
            If HolzShots.My.Settings.EllipseWidth > 100 OrElse HolzShots.My.Settings.EllipseWidth <= 0 Then
                HolzShots.My.Settings.EllipseWidth = 20
                HolzShots.My.Settings.Save()
            End If
            ThePanel.UseBoxInsteadOfCirlce = HolzShots.My.Settings.UseBoxInsteadOfCirlce
            EllipseOrRectangle.Value = If(ThePanel.UseBoxInsteadOfCirlce, 1, 0)
            EllipseBar.Value = HolzShots.My.Settings.EllipseWidth
            Ellipse_Width.Text = $"{EllipseBar.Value}px"
            ThePanel.EllipseColor = Ellipse_Viewer.Color
            ThePanel.EllipseWidth = EllipseBar.Value
            'Ellips_style.UseCompatibleTextRendering = True
        End Sub

        Private Sub LoadEraser()
            If HolzShots.My.Settings.EraserDiameter > 100 OrElse HolzShots.My.Settings.EraserDiameter <= 0 Then
                HolzShots.My.Settings.EraserDiameter = 20
                HolzShots.My.Settings.Save()
            End If
            EraserBar.Value = HolzShots.My.Settings.EraserDiameter
            Eraser_Diameter.Text = $"{ EraserBar.Value}px"
            ThePanel.EraserDiameter = EraserBar.Value
        End Sub

        Private Sub LoadBrighten()
            Dim v As Integer = HolzShots.My.Settings.BrightenColor.A
            If HolzShots.My.Settings.BrightenColor.R = HolzShots.My.Settings.BrightenColor.G AndAlso
                HolzShots.My.Settings.BrightenColor.R = HolzShots.My.Settings.BrightenColor.B Then
                If HolzShots.My.Settings.BrightenColor.R = 255 Then
                    v += 255
                ElseIf HolzShots.My.Settings.BrightenColor.R = 0 Then
                    v = 255 - v
                End If
            End If
            BlackWhiteTracker.Value = v
            BlackWhiteTrackerScroll(Nothing, Nothing)
        End Sub

#End Region

#End Region

#Region "Image Actions"


        Private Sub SaveImage()
            Using sfd As New SaveFileDialog()
                sfd.Filter = $"{Localization.PngImage}|*.png|{Localization.JpgImage}|*.jpg"
                sfd.DefaultExt = ".png"
                sfd.CheckPathExists = True
                sfd.Title = Localization.ChooseDestinationFileName
                Dim res = sfd.ShowDialog()
                If res = DialogResult.OK Then
                    Dim f = sfd.FileName
                    If String.IsNullOrWhiteSpace(f) Then Return
                    SaveImage(f)
                End If
            End Using
        End Sub

        Private Sub CopyImage()
            Dim bmp = ThePanel.CombinedImage
            Try
                Clipboard.SetImage(bmp)
            Catch ex As Exception When _
                    TypeOf ex Is ExternalException _
                    OrElse TypeOf ex Is System.Threading.ThreadStateException _
                    OrElse TypeOf ex Is ArgumentNullException
                HumanInterop.CopyImageFailed(ex)
            End Try
        End Sub

#End Region

#Region "Filesystem"

        Private Sub SaveImage(ByVal fileName As String)
            If String.IsNullOrEmpty(fileName) Then Throw New ArgumentNullException(NameOf(fileName))
            Try
                Dim bmp = ThePanel.CombinedImage()
                Debug.Assert(bmp IsNot Nothing)

                Dim format = ImageFormatInformation.GetImageFormatFromFileName(fileName)
                Debug.Assert(format IsNot Nothing)

                Using fileStream = File.OpenWrite(fileName)
                    bmp.SaveExtended(fileStream, format)
                End Using

                If _settingsContext.CloseAfterSave Then
                    Close()
                End If

            Catch ex As PathTooLongException
                HumanInterop.PathIsTooLong(fileName, Me)
            Catch ex As Exception
                HumanInterop.ErrorSavingImage(ex, Me)
            End Try
        End Sub

#End Region

#Region "UI-Events"

        Private Sub CopyToClipboardClick(ByVal sender As Object, ByVal e As EventArgs) Handles CopyToClipboard.Click
            CopyImage()
        End Sub

        Private Sub SaveBtnClick(ByVal sender As Object, ByVal e As EventArgs) Handles save_btn.Click
            SaveImage()
        End Sub

        Private Sub PrintClick(ByVal sender As Object, ByVal e As EventArgs) Handles Print.Click
            If DruckDialog.ShowDialog = DialogResult.OK Then
                DruckTeil.PrinterSettings = DruckDialog.PrinterSettings
                DruckTeil.DocumentName = $"HolzShots - Screenshot [{DateTime.Now:hh:mm:ss}]"
                DruckTeil.Print()
            End If
        End Sub

#End Region

#Region "Updater"

        Private Sub UpdateSettings()
            HolzShots.My.Settings.ZensursulaColor = Zensursula_Viewer.Color
            HolzShots.My.Settings.ZensursulaWidth = ZensursulaBar.Value

            HolzShots.My.Settings.MarkerColor = Marker_Viewer.Color
            HolzShots.My.Settings.MarkerWidth = MarkerBar.Value

            HolzShots.My.Settings.EraserDiameter = EraserBar.Value

            HolzShots.My.Settings.EllipseColor = Ellipse_Viewer.Color
            HolzShots.My.Settings.EllipseWidth = EllipseBar.Value

            HolzShots.My.Settings.BrightenColor = BigColorViewer1.Color

            HolzShots.My.Settings.ArrowColor = ArrowColorviewer.Color
            HolzShots.My.Settings.ArrowWidth = ArrowWidthSlider.Value
            HolzShots.My.Settings.UseBoxInsteadOfCirlce = ThePanel.UseBoxInsteadOfCirlce

            HolzShots.My.Settings.BlurFactor = ThePanel.BlurFactor


            HolzShots.My.Settings.Save()

        End Sub

        Private Sub ResetTools()
            ThePanel.CurrentTool = PaintPanel.ShotEditorTool.None
            CensorTool.Checked = False
            MarkerTool.Checked = False
            TextToolButton.Checked = False
            CroppingTool.Checked = False
            ArrowTool.Checked = False
            EraserTool.Checked = False
            BlurTool.Checked = False
            EllipseTool.Checked = False
            PipettenTool.Checked = False
            BrightenTool.Checked = False
            ScaleTool.Checked = False
            _activator.HideAll()
        End Sub

#End Region

#Region "Painting Tools"

        Private Sub AddSettingsPanels()
            _activator = New PanelActivator(Me)
            _activator.AddPanel(PaintPanel.ShotEditorTool.Arrow, ArrowSettingsPanel)
            _activator.AddPanel(PaintPanel.ShotEditorTool.Brighten, BrightenSettingsPanel)
            _activator.AddPanel(PaintPanel.ShotEditorTool.Blur, BlurSettingsPanel)
            _activator.AddPanel(PaintPanel.ShotEditorTool.Ellipse, EllipseSettingsPanel)
            _activator.AddPanel(PaintPanel.ShotEditorTool.Eraser, EraserSettingsPanel)
            _activator.AddPanel(PaintPanel.ShotEditorTool.Marker, MarkerSettingsPanel)
            _activator.AddPanel(PaintPanel.ShotEditorTool.Censor, CensorSettingsPanel)
        End Sub

        Private Sub PipettenToolClick(ByVal sender As Object, ByVal e As EventArgs) Handles PipettenTool.Click
            If CurrentTool = PaintPanel.ShotEditorTool.Pipette Then
                ResetTools()
            Else
                ThePanel.CurrentTool = PaintPanel.ShotEditorTool.Pipette
                MarkerTool.Checked = False
                CensorTool.Checked = False
                TextToolButton.Checked = False
                CroppingTool.Checked = False
                ArrowTool.Checked = False
                EraserTool.Checked = False
                BlurTool.Checked = False
                EllipseTool.Checked = False
                PipettenTool.Checked = True
                BrightenTool.Checked = False
                _activator.HideAll()
            End If
        End Sub

        Private Sub ScaleToolClick(sender As Object, e As EventArgs) Handles ScaleTool.Click
            If CurrentTool = PaintPanel.ShotEditorTool.Scale Then
                ResetTools()
            Else
                ThePanel.CurrentTool = PaintPanel.ShotEditorTool.Scale
                MarkerTool.Checked = False
                CensorTool.Checked = False
                TextToolButton.Checked = False
                CroppingTool.Checked = False
                ArrowTool.Checked = False
                EraserTool.Checked = False
                BlurTool.Checked = False
                EllipseTool.Checked = False
                PipettenTool.Checked = False
                BrightenTool.Checked = False
                ScaleTool.Checked = False
                _activator.HideAll()
            End If
        End Sub

        Private Sub CircleToolClick(sender As Object, e As EventArgs) Handles EllipseTool.Click
            If CurrentTool = PaintPanel.ShotEditorTool.Ellipse Then
                ResetTools()
            Else
                ThePanel.CurrentTool = PaintPanel.ShotEditorTool.Ellipse
                _activator.ActivateSettingsPanel(ThePanel.CurrentTool)
                MarkerTool.Checked = False
                CensorTool.Checked = False
                TextToolButton.Checked = False
                CroppingTool.Checked = False
                ArrowTool.Checked = False
                EraserTool.Checked = False
                BlurTool.Checked = False
                EllipseTool.Checked = True
                PipettenTool.Checked = False
                BrightenTool.Checked = False
                ScaleTool.Checked = False
            End If
        End Sub

        Private Sub TextToolButtonClick(sender As Object, e As EventArgs) Handles TextToolButton.Click
            If CurrentTool = PaintPanel.ShotEditorTool.Text Then
                ResetTools()
            Else
                ThePanel.CurrentTool = PaintPanel.ShotEditorTool.Text
                MarkerTool.Checked = False
                CensorTool.Checked = False
                TextToolButton.Checked = True
                CroppingTool.Checked = False
                ArrowTool.Checked = False
                EraserTool.Checked = False
                BlurTool.Checked = False
                EllipseTool.Checked = False
                PipettenTool.Checked = False
                BrightenTool.Checked = False
                ScaleTool.Checked = False
                _activator.HideAll()
            End If
        End Sub

        Private Sub EraserButtonClick(sender As Object, e As EventArgs) Handles EraserTool.Click
            If CurrentTool = PaintPanel.ShotEditorTool.Eraser Then
                ResetTools()
            Else
                ThePanel.CurrentTool = PaintPanel.ShotEditorTool.Eraser
                _activator.ActivateSettingsPanel(ThePanel.CurrentTool)
                MarkerTool.Checked = False
                CensorTool.Checked = False
                TextToolButton.Checked = False
                CroppingTool.Checked = False
                ArrowTool.Checked = False
                EraserTool.Checked = True
                BlurTool.Checked = False
                EllipseTool.Checked = False
                PipettenTool.Checked = False
                BrightenTool.Checked = False
                ScaleTool.Checked = False
            End If
        End Sub

        Private Sub CroppingToolClick(sender As Object, e As EventArgs) Handles CroppingTool.Click
            If CurrentTool = PaintPanel.ShotEditorTool.Crop Then
                ResetTools()
            Else
                ThePanel.CurrentTool = PaintPanel.ShotEditorTool.Crop
                MarkerTool.Checked = False
                CensorTool.Checked = False
                TextToolButton.Checked = False
                CroppingTool.Checked = True
                ArrowTool.Checked = False
                EraserTool.Checked = False
                BlurTool.Checked = False
                EllipseTool.Checked = False
                PipettenTool.Checked = False
                BrightenTool.Checked = False
                ScaleTool.Checked = False
                _activator.HideAll()
            End If
        End Sub

        Private Sub ArrowToolClick(sender As Object, e As EventArgs) Handles ArrowTool.Click
            If CurrentTool = PaintPanel.ShotEditorTool.Arrow Then
                ResetTools()
            Else
                ThePanel.CurrentTool = PaintPanel.ShotEditorTool.Arrow
                _activator.ActivateSettingsPanel(ThePanel.CurrentTool)
                MarkerTool.Checked = False
                CensorTool.Checked = False
                TextToolButton.Checked = False
                CroppingTool.Checked = False
                ArrowTool.Checked = True
                EraserTool.Checked = False
                BlurTool.Checked = False
                EllipseTool.Checked = False
                PipettenTool.Checked = False
                BrightenTool.Checked = False
                ScaleTool.Checked = False
            End If
        End Sub

        Private Sub ZensursulaClick(sender As Object, e As EventArgs) Handles CensorTool.Click
            If CurrentTool = PaintPanel.ShotEditorTool.Censor Then
                ResetTools()
            Else
                ThePanel.CurrentTool = PaintPanel.ShotEditorTool.Censor
                _activator.ActivateSettingsPanel(ThePanel.CurrentTool)
                CensorTool.Checked = True
                MarkerTool.Checked = False
                TextToolButton.Checked = False
                CroppingTool.Checked = False
                ArrowTool.Checked = False
                EraserTool.Checked = False
                BlurTool.Checked = False
                EllipseTool.Checked = False
                PipettenTool.Checked = False
                BrightenTool.Checked = False
                ScaleTool.Checked = False
            End If
        End Sub

        Private Sub HighlightClick(sender As Object, e As EventArgs) Handles MarkerTool.Click
            If CurrentTool = PaintPanel.ShotEditorTool.Marker Then
                ResetTools()
            Else
                ThePanel.CurrentTool = PaintPanel.ShotEditorTool.Marker
                _activator.ActivateSettingsPanel(ThePanel.CurrentTool)
                MarkerTool.Checked = True
                CensorTool.Checked = False
                TextToolButton.Checked = False
                CroppingTool.Checked = False
                ArrowTool.Checked = False
                EraserTool.Checked = False
                BlurTool.Checked = False
                EllipseTool.Checked = False
                PipettenTool.Checked = False
                BrightenTool.Checked = False
                ScaleTool.Checked = False
            End If
        End Sub

        Private Sub PixelateAreaClick(sender As Object, e As EventArgs) Handles BlurTool.Click
            If CurrentTool = PaintPanel.ShotEditorTool.Blur Then
                ResetTools()
            Else
                ThePanel.CurrentTool = PaintPanel.ShotEditorTool.Blur
                _activator.ActivateSettingsPanel(ThePanel.CurrentTool)
                MarkerTool.Checked = False
                CensorTool.Checked = False
                TextToolButton.Checked = False
                CroppingTool.Checked = False
                ArrowTool.Checked = False
                EraserTool.Checked = False
                BlurTool.Checked = True
                EllipseTool.Checked = False
                PipettenTool.Checked = False
                BrightenTool.Checked = False
                ScaleTool.Checked = False
            End If
        End Sub

        Private Sub BrightenToolClick(sender As Object, e As EventArgs) Handles BrightenTool.Click
            If CurrentTool = PaintPanel.ShotEditorTool.Brighten Then
                ResetTools()
            Else
                ThePanel.CurrentTool = PaintPanel.ShotEditorTool.Brighten
                _activator.ActivateSettingsPanel(ThePanel.CurrentTool)
                MarkerTool.Checked = False
                CensorTool.Checked = False
                TextToolButton.Checked = False
                CroppingTool.Checked = False
                ArrowTool.Checked = False
                EraserTool.Checked = False
                BlurTool.Checked = False
                EllipseTool.Checked = False
                PipettenTool.Checked = False
                BrightenTool.Checked = True
                ScaleTool.Checked = False
            End If
        End Sub

        Private Sub UndoStuffClick(sender As Object, e As EventArgs) Handles UndoStuff.Click
            ThePanel.Undo()
        End Sub

#End Region

#Region "Drucken"

        Private Sub DruckTeilPrintPage(sender As Object, e As PrintPageEventArgs) Handles DruckTeil.PrintPage
            Dim bmp = ThePanel.CombinedImage
            e.Graphics.DrawImage(bmp, e.PageBounds.Location)
        End Sub

#End Region

#Region "ShortcutKeys"

        Private Sub ZensToolStripMenuItemClick(sender As Object, e As EventArgs) Handles ZensToolStripMenuItem.Click
            CensorTool.PerformClick()
        End Sub

        Private Sub MarkToolStripMenuItemClick(sender As Object, e As EventArgs) Handles MarkToolStripMenuItem.Click
            MarkerTool.PerformClick()
        End Sub

        Private Sub TextToolStripMenuItemClick(sender As Object, e As EventArgs) Handles TextToolStripMenuItem.Click
            TextToolButton.PerformClick()
        End Sub

        Private Sub CropToolStripMenuItemClick(sender As Object, e As EventArgs) Handles CropToolStripMenuItem.Click
            CroppingTool.PerformClick()
        End Sub

        Private Sub EraseToolStripMenuItemClick(sender As Object, e As EventArgs) Handles EraseToolStripMenuItem.Click
            EraserTool.PerformClick()
        End Sub

        Private Sub PixelateToolStripMenuItemClick(sender As Object, e As EventArgs) Handles PixelateToolStripMenuItem.Click
            BlurTool.PerformClick()
        End Sub

        Private Sub ArrowToolStripMenuItemClick(sender As Object, e As EventArgs) Handles ArrowToolStripMenuItem.Click
            ArrowTool.PerformClick()
        End Sub

        Private Sub ResetToolStripMenuItemClick(sender As Object, e As EventArgs) Handles ResetToolStripMenuItem.Click
            UndoStuff.PerformClick()
        End Sub

        Private Sub UploadToolStripMenuItemClick(sender As Object, e As EventArgs) Handles UploadToolStripMenuItem.Click
            UploadToHoster.PerformButtonClick()
        End Sub

        Private Sub SaveToolStripMenuItemClick(sender As Object, e As EventArgs) Handles SaveToolStripMenuItem.Click
            save_btn.PerformClick()
        End Sub

        Private Sub ClipboardToolStripMenuItemClick(sender As Object, e As EventArgs) Handles ClipboardToolStripMenuItem.Click
            CopyToClipboard.PerformClick()
        End Sub

        Private Sub PrintToolStripMenuItemClick(sender As Object, e As EventArgs) Handles PrintToolStripMenuItem.Click
            Print.PerformClick()
        End Sub

        Private Sub KreisToolStripMenuItemClick(sender As Object, e As EventArgs) Handles KreisToolStripMenuItem.Click
            EllipseTool.PerformClick()
        End Sub

#End Region

#Region "Toolsettings"

        Private Sub ZensursulaBarValueChanged(sender As Object, e As EventArgs) Handles ZensursulaBar.Scroll
            Pinsel_Width_Zensursula.Text = $"{ZensursulaBar.Value}px"
            ThePanel.ZensursulaWidth = ZensursulaBar.Value
        End Sub

        Private Sub ZensursulaViewerColorChanged(sender As Object, c As Color) Handles Zensursula_Viewer.ColorChanged
            ThePanel.ZensursulaColor = c
        End Sub

        Private Sub MarkerBarValueChanged(sender As Object, e As EventArgs) Handles MarkerBar.Scroll
            Pinsel_Width_Marker.Text = $"{MarkerBar.Value}px"
            ThePanel.MarkerWidth = MarkerBar.Value
        End Sub

        Private Sub MarkerViewerColorChanged(sender As Object, c As Color) Handles Marker_Viewer.ColorChanged
            ThePanel.MarkerColor = c
        End Sub

        Private Sub EraserBarScroll(sender As Object, e As EventArgs) Handles EraserBar.ValueChanged
            Eraser_Diameter.Text = $"{EraserBar.Value}px"
            ThePanel.EraserDiameter = EraserBar.Value
        End Sub

        Private Sub EllipseBarValueChanged(sender As Object, e As EventArgs) Handles EllipseBar.ValueChanged
            Ellipse_Width.Text = $"{EllipseBar.Value}px"
            ThePanel.EllipseWidth = EllipseBar.Value
        End Sub

        Private Sub EllipseViewerColorChanged(sender As Object, c As Color) Handles Ellipse_Viewer.ColorChanged
            ThePanel.EllipseColor = c
        End Sub

#End Region

        Private Sub ImageInfoLabelMouseClick(sender As Object, e As MouseEventArgs) Handles ImageInfoLabel.MouseUp

            Dim s = ThePanel.Screenshot
            If e.Button = MouseButtons.Left Then
                ClipboardEx.SetText($"{s.Size.Width}x{s.Size.Height}px")
            ElseIf e.Button = MouseButtons.Right Then
                ClipboardEx.SetText(s.Timestamp.ToString())
            ElseIf e.Button = MouseButtons.Middle Then
                ClipboardEx.SetText($"{s.Timestamp} {s.Size.Width}x{s.Size.Height}px")
            End If
        End Sub

        Private Sub ThePanelInitialized() Handles ThePanel.Initialized
            ImageInfoLabel.Text = ThePanel.SizeInfo
            ImageInfoLabel.ToolTipText = ThePanel.SizeInfoText
            MouseInfoLabel.Text = "0, 0px"
        End Sub

        Private Sub ThePanelUpdateMousePosition(e As Point) Handles ThePanel.UpdateMousePosition
            MouseInfoLabel.Text = $"{e.X}, {e.Y}px"
        End Sub


        Private Sub ChooseServiceToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ChooseServiceToolStripMenuItem.Click
        End Sub

        Private Sub BlackWhiteTrackerScroll(sender As Object, e As EventArgs) Handles BlackWhiteTracker.Scroll
            If BlackWhiteTracker.Value >= 0 AndAlso BlackWhiteTracker.Value <= 255 Then
                ThePanel.BrightenColor = Color.FromArgb(255 - BlackWhiteTracker.Value, 0, 0, 0)
                BigColorViewer1.Color = ThePanel.BrightenColor
            ElseIf BlackWhiteTracker.Value > 255 AndAlso BlackWhiteTracker.Value <= 510 Then
                ThePanel.BrightenColor = Color.FromArgb(BlackWhiteTracker.Value - 255, 255, 255, 255)
                BigColorViewer1.Color = ThePanel.BrightenColor
            End If
        End Sub

        Private Sub ArrowColorviewerColorChanged(sender As Object, c As Color) Handles ArrowColorviewer.ColorChanged
            ThePanel.ArrowColor = c
        End Sub

        Private Sub DrawCursorClick(sender As Object, e As EventArgs) Handles DrawCursor.Click
            ThePanel.DrawCursor = DrawCursor.Checked
        End Sub

        Private Sub ArrowWidthSliderScroll(sender As Object, e As EventArgs) Handles ArrowWidthSlider.Scroll
            ArrowWidthLabel.Text = If(ArrowWidthSlider.Value = 0, "Auto", $"{ArrowWidthSlider.Value}px")
            ThePanel.ArrowWidth = ArrowWidthSlider.Value
        End Sub

        Private Sub ShotEditorResize(sender As Object, e As EventArgs) Handles Me.Resize
            ThePanel.VerticalLinealBox.Invalidate()
            ThePanel.HorizontalLinealBox.Invalidate()
        End Sub

        Private Sub ToolStripsPaint(sender As Object, e As PaintEventArgs) Handles ToolStrip1.Paint, ShareStrip.Paint, EditStrip.Paint, CopyPrintToolStrip.Paint
            e.Graphics.Clear(BackColor)
        End Sub

        Private Async Sub UploadToHosterDropDownItemClicked(sender As Object, e As ToolStripItemClickedEventArgs) Handles UploadToHoster.DropDownItemClicked

            Dim tag = DirectCast(e.ClickedItem.Tag, String)
            ' the tag represents the name of the image hoster here
            If String.IsNullOrWhiteSpace(tag) Then Return

            ' Dirty :>
            Dim info = HolzShots.My.Application.Uploaders.GetUploaderByName(tag)

            Debug.Assert(Not String.IsNullOrEmpty(tag))
            Debug.Assert(info IsNot Nothing)
            Debug.Assert(info.Metadata IsNot Nothing)
            Debug.Assert(info.Uploader IsNot Nothing)
            Debug.Assert(Uploader.HasEqualName(info.Metadata.Name, tag))

            Dim image = ThePanel.CombinedImage
            Dim format = UploadHelper.GetImageFormat(image, _settingsContext)
            Try
                Dim result = Await UploadHelper.Upload(info.Uploader, image, _settingsContext, format, Me).ConfigureAwait(True)
                Debug.Assert(result IsNot Nothing)
                UploadHelper.InvokeUploadFinishedUi(result, _settingsContext)
            Catch ex As UploadCanceledException
                HumanInterop.ShowOperationCanceled()
            Catch ex As UploadException
                HumanInterop.UploadFailed(ex)
                Return
            End Try
            HandleAfterUpload()
        End Sub

        Private Sub UploadToHosterButtonClick(sender As Object, e As EventArgs) Handles UploadToHoster.ButtonClick
            UploadCurrentImageToDefaultProvider()
        End Sub

        Private Sub EllipseOrRectangleValueChanged(sender As Object, e As EventArgs) Handles EllipseOrRectangle.ValueChanged
            ThePanel.UseBoxInsteadOfCirlce = EllipseOrRectangle.Value = 1
            EllipseOrRectangleBox.Invalidate()
        End Sub

        Private Sub EllipseOrRectangleBoxClick(sender As Object, e As EventArgs) Handles EllipseOrRectangleBox.Click
            If EllipseOrRectangle.Value = 1 Then EllipseOrRectangle.Value = 0 Else EllipseOrRectangle.Value = 1
        End Sub

        Private Sub EllipseOrRectangleBoxPaint(sender As Object, e As PaintEventArgs) Handles EllipseOrRectangleBox.Paint
            Dim rct As New Rectangle(2, 2, 12, 12)
            Dim pe As New Pen(Brushes.Red) With {.Width = 2}
            If ThePanel.UseBoxInsteadOfCirlce Then
                e.Graphics.DrawRectangle(pe, rct)
            Else
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality
                e.Graphics.DrawEllipse(pe, rct)
            End If
        End Sub

        Private Sub HandleAfterUpload()
            If _settingsContext.CloseAfterUpload Then
                Close()
            End If
        End Sub

        Private Sub BlurnessBarValueChanged(sender As Object, e As EventArgs) Handles BlurnessBar.ValueChanged
            ThePanel.BlurFactor = BlurnessBar.Value
        End Sub

        Private Async Sub UploadCurrentImageToDefaultProvider()
            Dim image = ThePanel.CombinedImage
            Dim format = UploadHelper.GetImageFormat(image, _settingsContext)
            Try
                Dim result = Await UploadHelper.UploadToDefaultUploader(ThePanel.CombinedImage, _settingsContext, format, Me).ConfigureAwait(True)
                Debug.Assert(result IsNot Nothing)
                UploadHelper.InvokeUploadFinishedUi(result, _settingsContext)
            Catch ex As UploadCanceledException
                HumanInterop.ShowOperationCanceled()
            Catch ex As UploadException
                HumanInterop.UploadFailed(ex)
                Return
            Finally
                HandleAfterUpload()
            End Try
        End Sub

        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            Try
                If disposing AndAlso components IsNot Nothing Then
                    components?.Dispose()
                    _uploadThumbnailButton?.Dispose()
                    _saveThumbnailButton?.Dispose()
                    _copyThumbnailButton?.Dispose()
                End If
            Finally
                MyBase.Dispose(disposing)
            End Try
        End Sub
    End Class
End Namespace
