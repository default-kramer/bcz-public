== 
My thoughts on how to manage Godot theme-related stuff.
As I accumulate personal best practices, add them to this file.
==


Don't directly consume a font.
Instead, put a .tres file into the /fonts directory and then reference that.
A name like "NormalButtonFont.tres" is probably better than "MulishSemibold18.tres".
Rationale:
* Only need to update one place
* Make it easier to determine which fonts are actually being used


Note to self:
I thought I would make use of "Theme Type Variations" which appear to be the closest thing to a CSS class.
But so far I haven't needed them.
