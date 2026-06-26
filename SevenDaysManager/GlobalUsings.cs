// Explicit aliases so WPF types win over WinForms when UseWindowsForms=true is set
global using Application    = System.Windows.Application;
global using Color          = System.Windows.Media.Color;
global using UserControl    = System.Windows.Controls.UserControl;
global using KeyEventArgs   = System.Windows.Input.KeyEventArgs;
global using MessageBox     = System.Windows.MessageBox;
global using Clipboard      = System.Windows.Clipboard;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
