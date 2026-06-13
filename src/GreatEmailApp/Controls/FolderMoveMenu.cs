// FILE: src/GreatEmailApp/Controls/FolderMoveMenu.cs
// Created: 2026-06-12 | Revised: 2026-06-12 | Rev: 1
// Changed by: Claude Opus 4.8 on behalf of James Reed
//
// Shared builder for the "Move to…" folder picker. Used by both the mail-list
// right-click context menu (MailList) and the ribbon Move button (Ribbon) so
// the two surfaces stay identical — account headers, nested subfolder
// submenus, cross-account moves blocked. Extracted from MailList.xaml.cs
// (was BuildFolderMenuItem) when the ribbon Move button got wired up (P1-4).

using System.Collections.Generic;
using System.Windows.Controls;
using GreatEmailApp.ViewModels;

namespace GreatEmailApp.Controls;

internal static class FolderMoveMenu
{
    /// <summary>
    /// Build the account-grouped folder <see cref="MenuItem"/>s for moving
    /// <paramref name="msg"/>. Only the message's own account is offered (IMAP
    /// can't move across accounts in a single command). A null message yields
    /// every account's tree but the click is a no-op without a target.
    /// </summary>
    public static IEnumerable<MenuItem> BuildItems(MainViewModel vm, MessageViewModel? msg)
    {
        foreach (var account in vm.Accounts)
        {
            if (msg is not null && account.Model.Id != msg.Model.AccountId) continue;

            yield return new MenuItem
            {
                Header = account.EmailAddress,
                IsEnabled = false,
                Tag = "header",
            };

            foreach (var folder in account.Folders)
            {
                var built = BuildFolderMenuItem(folder, vm, msg);
                if (built is not null) yield return built;
            }
        }
    }

    /// <summary>
    /// Build a MenuItem for <paramref name="folder"/>, recursively attaching
    /// child folders as a nested submenu. Returns null for folders we skip
    /// (Outbox — no IMAP path). A folder with children is itself clickable
    /// (move into the parent) AND opens a submenu on hover for the children.
    /// </summary>
    private static MenuItem? BuildFolderMenuItem(FolderViewModel folder, MainViewModel vm, MessageViewModel? msg)
    {
        if (string.IsNullOrEmpty(folder.Model.FullPath)) return null;

        var item = new MenuItem { Header = folder.Name };
        item.Click += (_, args) =>
        {
            // A click on a parent folder bubbles up from child clicks too —
            // only act when this MenuItem itself was the source.
            if (args.OriginalSource != item) return;
            if (msg is not null) vm.MoveToFolderCommand.Execute((msg, folder));
        };

        foreach (var child in folder.Children)
        {
            var childItem = BuildFolderMenuItem(child, vm, msg);
            if (childItem is not null) item.Items.Add(childItem);
        }

        return item;
    }
}
