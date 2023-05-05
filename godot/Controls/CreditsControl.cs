using Godot;
using System;

public class CreditsControl : Control
{
    static readonly int CopyrightYear = DateTime.Now.Year;

    readonly struct Members
    {
        public readonly TabContainer TabContainer;
        public readonly MenuChoiceControl TabCycler;

        public Members(Control me)
        {
            me.FindNode(out TabContainer, nameof(TabContainer));
            me.FindNode(out TabCycler, nameof(TabCycler));
        }
    }

    /// <summary>
    /// Use up/down to scroll the text.
    /// </summary>
    public override void _Process(float delta)
    {
        const int scrollSpeed = 500; // units unknown, chosen via experimentation
        int scrollChange = 0;
        if (Input.IsActionPressed("ui_down"))
        {
            scrollChange = 1;
        }
        else if (Input.IsActionPressed("ui_up"))
        {
            scrollChange = -1;
        }
        if (scrollChange != 0)
        {
            int tabIndex = members.TabContainer.CurrentTab;
            var child = (RichTextLabel)members.TabContainer.GetChild(tabIndex);
            var scrollBar = child.GetVScroll();
            scrollBar.Value += scrollChange * delta * scrollSpeed;
        }
    }

    /// <summary>
    /// The left/right actions switch tabs.
    /// The up/down actions scroll the text.
    /// Pretty much every other action should exit back to the title screen.
    /// </summary>
    public override void _Input(InputEvent e)
    {
        if (e.IsActionPressed("ui_accept")
            || e.IsActionPressed("ui_select")
            || e.IsActionPressed("ui_cancel")
            || e.IsActionPressed("game_rotate_cw")
            || e.IsActionPressed("game_rotate_ccw")
            || e.IsActionPressed("game_drop"))
        {
            // Mark this as handled, otherwise the main menu will handle it.
            GetTree().SetInputAsHandled();
            NewRoot.FindRoot(this).BackToMainMenu();
        }
    }

    private readonly ChoiceModel<string> choiceModel = new ChoiceModel<string>();
    private Members members;
    public override void _Ready()
    {
        members = new Members(this);
        AddTab("Block Cipher Z", BlockCipherZInfo);
        AddTab("Godot Engine", GodotEngineLicense);
        AddTab("FreeType", FreeTypeLicense);
        AddTab("ENet", ENetLicense);
        AddTab("Mbed TLS", MbedTlsLicense);
        AddTab("Fonts", OFL_v1_1);

        members.TabCycler.Model = choiceModel;
        choiceModel.OnChanged(() =>
        {
            members.TabContainer.CurrentTab = choiceModel.SelectedIndex;
        });
    }

    private void AddTab(string title, string text)
    {
        int index = members.TabContainer.GetChildCount();

        var label = new RichTextLabel();
        label.SelectionEnabled = true;
        label.FocusMode = FocusModeEnum.None; // Don't allow focus, otherwise you can't get back out using up/down navigation
        label.Text = text;
        members.TabContainer.AddChild(label);
        members.TabContainer.SetTabTitle(index, title);
        choiceModel.AddChoice(title);
    }

    public void OnShown()
    {
        members.TabCycler.GrabFocus();
    }

    readonly string BlockCipherZInfo = @$"Block Cipher Z copyright © {CopyrightYear} Ryan Kramer.

This game contains software written by third parties. For more information and licenses, use left/right to view the other tabs.";

    /// <summary>
    /// Per https://docs.godotengine.org/en/4.0/about/complying_with_licenses.html#requirements
    ///   In the case of the MIT license, the only requirement is to include the license text somewhere in your game or derivative project.
    ///   This text reads as follows:
    /// </summary>
    const string GodotEngineLicense = @"This game uses Godot Engine, available under the following license:

Copyright (c) 2014-present Godot Engine contributors. Copyright (c) 2007-2014 Juan Linietsky, Ariel Manzur.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the ""Software""), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";

    /// <summary>
    /// Per https://docs.godotengine.org/en/4.0/about/complying_with_licenses.html#third-party-licenses
    ///   Godot uses FreeType to render fonts.
    ///   Its license requires attribution, so the following text must be included together with the Godot license:
    /// </summary>
    readonly string FreeTypeLicense = $"Portions of this software are copyright © {CopyrightYear} The FreeType Project (www.freetype.org). All rights reserved.";

    /// <summary>
    /// Per https://docs.godotengine.org/en/4.0/about/complying_with_licenses.html#third-party-licenses
    ///   Godot includes the ENet library to handle high-level multiplayer.
    ///   ENet has similar licensing terms as Godot:
    /// </summary>
    const string ENetLicense = @"Copyright (c) 2002-2020 Lee Salzman

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the ""Software""), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";

    /// <summary>
    /// Per https://docs.godotengine.org/en/4.0/about/complying_with_licenses.html#third-party-licenses
    ///   If the project is exported with Godot 3.1 or later, it includes mbed TLS.
    ///   The Apache license needs to be complied to by including the following text:
    /// </summary>
    const string MbedTlsLicense = @"Copyright The Mbed TLS Contributors

Licensed under the Apache License, Version 2.0 (the ""License""); you may not use this file except in compliance with the License. You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an ""AS IS"" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.";

    /// <summary>
    /// Use a single tab for all fonts using: SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007
    /// </summary>
    const string OFL_v1_1 = @"This game uses the following fonts under the SIL Open Font License, Version 1.1:

Mulish
   Copyright 2016 The Mulish Project Authors (https://github.com/googlefonts/mulish)

   This Font Software is licensed under the SIL Open Font License, Version 1.1.
   This license is copied below, and is also available with a FAQ at:
   http://scripts.sil.org/OFL

Qaz
   Copyright © 2022 GGBotNet (https://ggbot.net/fonts), with Reserved Font Name ""Qaz"".

   This Font Software is licensed under the SIL Open Font License, Version 1.1.
   This license is copied below, and is also available with a FAQ at:
   http://scripts.sil.org/OFL

Yulong
   Copyright © 2022 GGBotNet (https://ggbot.net/fonts), with Reserved Font Name ""Yulong"".

   This Font Software is licensed under the SIL Open Font License, Version 1.1.
   This license is copied below, and is also available with a FAQ at:
   http://scripts.sil.org/OFL


-----------------------------------------------------------
SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007
-----------------------------------------------------------

PREAMBLE
The goals of the Open Font License (OFL) are to stimulate worldwide
development of collaborative font projects, to support the font creation
efforts of academic and linguistic communities, and to provide a free and
open framework in which fonts may be shared and improved in partnership
with others.

The OFL allows the licensed fonts to be used, studied, modified and
redistributed freely as long as they are not sold by themselves. The
fonts, including any derivative works, can be bundled, embedded, 
redistributed and/or sold with any software provided that any reserved
names are not used by derivative works. The fonts and derivatives,
however, cannot be released under any other type of license. The
requirement for fonts to remain under this license does not apply
to any document created using the fonts or their derivatives.

DEFINITIONS
""Font Software"" refers to the set of files released by the Copyright
Holder(s) under this license and clearly marked as such. This may
include source files, build scripts and documentation.

""Reserved Font Name"" refers to any names specified as such after the
copyright statement(s).

""Original Version"" refers to the collection of Font Software components as
distributed by the Copyright Holder(s).

""Modified Version"" refers to any derivative made by adding to, deleting,
or substituting -- in part or in whole -- any of the components of the
Original Version, by changing formats or by porting the Font Software to a
new environment.

""Author"" refers to any designer, engineer, programmer, technical
writer or other person who contributed to the Font Software.

PERMISSION & CONDITIONS
Permission is hereby granted, free of charge, to any person obtaining
a copy of the Font Software, to use, study, copy, merge, embed, modify,
redistribute, and sell modified and unmodified copies of the Font
Software, subject to the following conditions:

1) Neither the Font Software nor any of its individual components,
in Original or Modified Versions, may be sold by itself.

2) Original or Modified Versions of the Font Software may be bundled,
redistributed and/or sold with any software, provided that each copy
contains the above copyright notice and this license. These can be
included either as stand-alone text files, human-readable headers or
in the appropriate machine-readable metadata fields within text or
binary files as long as those fields can be easily viewed by the user.

3) No Modified Version of the Font Software may use the Reserved Font
Name(s) unless explicit written permission is granted by the corresponding
Copyright Holder. This restriction only applies to the primary font name as
presented to the users.

4) The name(s) of the Copyright Holder(s) or the Author(s) of the Font
Software shall not be used to promote, endorse or advertise any
Modified Version, except to acknowledge the contribution(s) of the
Copyright Holder(s) and the Author(s) or with their explicit written
permission.

5) The Font Software, modified or unmodified, in part or in whole,
must be distributed entirely under this license, and must not be
distributed under any other license. The requirement for fonts to
remain under this license does not apply to any document created
using the Font Software.

TERMINATION
This license becomes null and void if any of the above conditions are
not met.

DISCLAIMER
THE FONT SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT
OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE
COPYRIGHT HOLDER BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL
DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM
OTHER DEALINGS IN THE FONT SOFTWARE.";

}
