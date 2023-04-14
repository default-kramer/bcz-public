using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core.ReplayModel
{
    public readonly struct VersionElement
    {
        public readonly int VersionNumber;

        public VersionElement(int versionNumber)
        {
            this.VersionNumber = versionNumber;
        }
    }

    public readonly struct SettingsElement
    {
        public readonly string Name;
        public readonly string Value;

        public SettingsElement(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }
    }

    public readonly struct CommandElement
    {
        public readonly Command Command;
        public readonly Moment Moment;

        public CommandElement(Command command, Moment moment)
        {
            this.Command = command;
            this.Moment = moment;
        }
    }

    public readonly struct GridHashElement
    {
        public readonly int HashValue;
    }
}
