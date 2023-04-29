using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

public interface IChoiceModel
{
    void Next();
    void Previous();
    string DisplayValue { get; }
    IReadOnlyList<string> AllDisplayValues { get; }
    int SelectedIndex { get; }
}

public sealed class ChoiceModel<T> : IChoiceModel
{
    private readonly List<T> Choices = new List<T>();
    private readonly List<string> DisplayValues = new List<string>();
    public int SelectedIndex { get; private set; } = -1;
    public T SelectedItem { get { return Choices[SelectedIndex]; } }
    public string DisplayValue { get { return DisplayValues[SelectedIndex]; } }

    private Action<ChoiceModel<T>> onChanged = _ => { };

    public ChoiceModel<T> AddChoice(T item)
    {
        Choices.Add(item);
        DisplayValues.Add(item?.ToString() ?? "");
        if (Choices.Count == 1)
        {
            SetIndex(0);
        }
        return this;
    }

    public ChoiceModel<T> AddChoices(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            AddChoice(item);
        }
        return this;
    }

    public ChoiceModel<T> AddChoices(params T[] items)
    {
        return AddChoices(items as IEnumerable<T>);
    }

    public ChoiceModel<T> OnChanged(Action<ChoiceModel<T>> callback)
    {
        var temp = onChanged;
        onChanged = me => { temp(me); callback(me); };
        return this;
    }

    public ChoiceModel<T> OnChanged(Action callback)
    {
        return OnChanged(me => callback());
    }

    private void SetIndex(int index)
    {
        SelectedIndex = (index + Choices.Count) % Choices.Count;
        onChanged(this);
    }

    public void Next()
    {
        SetIndex(SelectedIndex + 1);
    }

    public void Previous()
    {
        SetIndex(SelectedIndex - 1);
    }

    public IReadOnlyList<string> AllDisplayValues
    {
        get { return DisplayValues; }
    }
}
