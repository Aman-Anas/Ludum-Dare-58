namespace Utilities.Logic;

using Godot;

public class SubMenuHelper
{
    public Control CurrentSubMenu { get; set; }

    public Control MainRoot { get; set; }

    public Button CloseButton { get; set; }

    public SubMenuHelper(Button closeButton = null, Control mainRoot = null)
    {
        MainRoot = mainRoot;
        CloseButton = closeButton;
        if (CloseButton != null)
        {
            CloseButton.Pressed += CloseSubMenu;
        }
    }

    public void SetSubMenu(Control subMenuRoot)
    {
        CurrentSubMenu?.Hide(); // Hide old if there is currently a sub menu open
        MainRoot?.Hide();
        subMenuRoot.Show();
        CurrentSubMenu = subMenuRoot;
        CloseButton?.Show();
    }

    public void CloseSubMenu()
    {
        CurrentSubMenu?.Hide();
        CurrentSubMenu = null;
        CloseButton?.Hide();
        MainRoot?.Show(); // If main root defined, show it again
    }
}
