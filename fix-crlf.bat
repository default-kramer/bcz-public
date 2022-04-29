rem It seems that VS ignores the `end_of_line` editorconfig setting when creating a new file (which sucks).
rem And it seems that once a file is created, VS will preserve the newline style that has already been established (which is reasonable).
rem So I set git autocrlf=input which should automatically correct newly-created files.
rem This also warns me when it's going to happen so I can run this script to clean up my local copy.
find . -name "*.cs" -print0 | xargs -0 dos2unix