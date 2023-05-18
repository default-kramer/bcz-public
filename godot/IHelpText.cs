using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

interface IHelpText
{
    void SetText(string? text);
}

class NullHelpText : IHelpText
{
    private NullHelpText() { }
    public static NullHelpText Instance = new NullHelpText();
    public void SetText(string? text) { }
}
