using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EmailAutomation.Domain.Models;

/// <summary>
/// Implements INotifyPropertyChanged because this model is bound directly in the UI (no separate
/// ViewModel wrapper exists for it) - without it, editing Name in the detail pane would not
/// refresh the matching row in the template list.
/// </summary>
public class EmailTemplate : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public Guid Id { get; set; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    private string _subjectTemplate = string.Empty;
    public string SubjectTemplate
    {
        get => _subjectTemplate;
        set
        {
            if (_subjectTemplate != value)
            {
                _subjectTemplate = value;
                OnPropertyChanged();
            }
        }
    }

    private string _bodyTemplate = string.Empty;
    public string BodyTemplate
    {
        get => _bodyTemplate;
        set
        {
            if (_bodyTemplate != value)
            {
                _bodyTemplate = value;
                OnPropertyChanged();
            }
        }
    }
}
