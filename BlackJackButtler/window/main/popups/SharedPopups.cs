namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    internal void DrawMainSharedPopups()
    {
        DrawAliasModal();
        DrawCleanDataPopup();
        DrawPanicPopups();
    }
}
