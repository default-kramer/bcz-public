NOTE TO SELF: Review requirements of /godot/licensed before deploying publicly.

Getting this autobuild working was not easy.
Read this carefully before trying to change it.


I tried using existing Docker images like
* https://github.com/aBARICHELLO/godot-ci
* https://github.com/zerc/docker-godot-mono
but these did not work.
I think the main reason is that they don't support C# langversion 10.
There might be other reasons.

I think that most Godot Mono users are probably just using the C# compiler version that comes
with Mono, but I think the Mono project has no intent to develop their own C# compiler any more
now that dotnet Core is the future.
But what do I know? I barely understand the dotnet variants and versions on Windows, much less on Linux.
