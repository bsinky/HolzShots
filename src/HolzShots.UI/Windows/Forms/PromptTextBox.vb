Imports System.ComponentModel
Imports System.Windows.Forms

Namespace Windows.Forms
    Public Class PromptTextBox
        Inherits TextBox

        Private _description As String

        <Browsable(True), Description("Der Text, der gr�ulich angezeigt wird, wenn kein Text eingegeben wurde. K�nnte f�r eine Kurzbeschreibung verwendet werden."), Category("Appearance")>
        Public Property Prompt As String
            Get
                Return _description
            End Get
            Set(ByVal value As String)
                _description = value
                UpdateMessage()
            End Set
        End Property

        Public Sub New()
            MyBase.New()
            UpdateMessage()
        End Sub

        Private Sub UpdateMessage()
            NativeMethods.SendMessage(Me.Handle, &H1500 + 1, IntPtr.Zero, _description)
        End Sub
    End Class
End Namespace
