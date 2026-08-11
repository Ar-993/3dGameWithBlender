Option Explicit

Dim shell, fileSystem, projectDirectory, contentProject, exitCode

Set shell = CreateObject("WScript.Shell")
Set fileSystem = CreateObject("Scripting.FileSystemObject")

projectDirectory = fileSystem.GetParentFolderName(WScript.ScriptFullName)
contentProject = fileSystem.BuildPath(projectDirectory, "Content\Content.mgcb")
shell.CurrentDirectory = projectDirectory

' Восстановление выполняется скрыто; при ошибке пользователь всё равно увидит сообщение.
exitCode = shell.Run("dotnet tool restore", 0, True)
If exitCode <> 0 Then
    MsgBox "Не удалось восстановить MGCB Editor. Проверьте установку .NET SDK.", vbCritical, "MGCB Editor"
    WScript.Quit exitCode
End If

' Консоль dotnet скрыта, а графическое окно MGCB Editor остаётся видимым.
shell.Run "dotnet mgcb-editor-windows """ & contentProject & """", 0, False
