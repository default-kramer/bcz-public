using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[Flags]
enum PauseMenuActions
{
    None = 0,
    Restart = 1,
    Quit = 2,
}

public enum Layout
{
    Tall,
    Wide,
}
