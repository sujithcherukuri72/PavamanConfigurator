using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PavamanDroneConfigurator.Core.Models;

public class DroneParameter : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private float _value;
    private float _originalValue;
    private string? _description;
    private float? _minValue;
    private float? _maxValue;
    private bool _isModified;

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

    public float Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                IsModified = _value != _originalValue;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The original value from the vehicle. Used to track if the parameter has been modified.
    /// </summary>
    public float OriginalValue
    {
        get => _originalValue;
        set
        {
            if (_originalValue != value)
            {
                _originalValue = value;
                IsModified = _value != _originalValue;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indicates whether the current value differs from the original value.
    /// </summary>
    public bool IsModified
    {
        get => _isModified;
        private set
        {
            if (_isModified != value)
            {
                _isModified = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
            }
        }
    }

    public float? MinValue
    {
        get => _minValue;
        set
        {
            if (_minValue != value)
            {
                _minValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RangeDisplay));
            }
        }
    }

    public float? MaxValue
    {
        get => _maxValue;
        set
        {
            if (_maxValue != value)
            {
                _maxValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RangeDisplay));
            }
        }
    }

    /// <summary>
    /// Gets a formatted display string for the parameter's valid range.
    /// Returns "NA" if no min/max values are defined.
    /// </summary>
    public string RangeDisplay
    {
        get
        {
            if (!MinValue.HasValue && !MaxValue.HasValue)
            {
                return "NA";
            }
            
            var minStr = MinValue.HasValue ? MinValue.Value.ToString() : "NA";
            var maxStr = MaxValue.HasValue ? MaxValue.Value.ToString() : "NA";
            
            return $"Min {minStr} - Max {maxStr}";
        }
    }

    /// <summary>
    /// Marks this parameter as saved by updating the original value to match the current value.
    /// </summary>
    public void MarkAsSaved()
    {
        _originalValue = _value;
        IsModified = false;
    }

    /// <summary>
    /// Reverts the value to the original value from the vehicle.
    /// </summary>
    public void RevertToOriginal()
    {
        Value = _originalValue;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
