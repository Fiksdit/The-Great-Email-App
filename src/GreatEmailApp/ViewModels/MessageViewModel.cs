// FILE: src/GreatEmailApp/ViewModels/MessageViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.ViewModels;

public partial class MessageViewModel : ObservableObject
{
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private bool isFirstInGroup;

    public Message Model { get; }

    public string Id => Model.Id;
    public string Sender => Model.Sender;
    public string SenderEmail => Model.SenderEmail;
    public string Avatar => Model.Avatar;
    public string Subject => Model.Subject;
    public string Preview => Model.Preview;
    public string Time => Model.Time;
    public string FullTime => string.IsNullOrEmpty(Model.FullTime) ? Model.Time : Model.FullTime;
    public string Group => Model.Group;
    public string To => Model.To;
    public string Cc => Model.Cc;

    public bool Unread => Model.Unread;
    public bool Flagged => Model.Flagged;
    public bool Important => Model.Important;
    public bool HasAttachments => Model.Attachments.Count > 0;
    public System.Collections.Generic.IReadOnlyList<Attachment> Attachments => Model.Attachments;

    public string BodyHtml => Model.BodyHtml;
    public string BodyPlain => Model.BodyPlain;
    public string BodyDisplay =>
        !string.IsNullOrEmpty(Model.BodyHtml) ? Model.BodyHtml :
        !string.IsNullOrEmpty(Model.BodyPlain) ? Model.BodyPlain :
        Model.Preview;

    public Brush AvatarBrush { get; }

    public MessageViewModel(Message model)
    {
        Model = model;

        var s = model.Color.TrimStart('#');
        if (s.Length == 3) s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        var c = Color.FromRgb(
            System.Convert.ToByte(s.Substring(0, 2), 16),
            System.Convert.ToByte(s.Substring(2, 2), 16),
            System.Convert.ToByte(s.Substring(4, 2), 16));
        var b = new SolidColorBrush(c);
        b.Freeze();
        AvatarBrush = b;
    }
}
