# README

First, extract the files.
Then, start the respective backend for your OS:

## Windows
1. Shift+Right Click the window with the extracted files.
2. Select "Open in Terminal", "Open PowerShell window here" or something of the sort.
3. Type (without the quotes) `.\backend-windows.exe` to run it.
4. See the output if it worked.

## macOS
1. Open a Terminal window (you can search it in Spotlight) and type `cd`.
2. Open the folder with the extracted files in the Finder.
3. Press and hold the folder icon next to the folder name at the window title until you "grab it".
4. Drag the icon to the Terminal. The path to the folder should be pasted.
5. Press Enter to go to the folder.
6. Type (without the quotes) `chmod +x backend-macos`.
7. Type (without the quotes) `./backend-macos` to run it.
8. See the output if it worked.

## Linux
come on you know how to use the terminal

If you are on macOS (or has some weird environment),
the backend may not find the installation automatically,
and will complain to you.

To fix this:
1. Find the path to the Deadlock folder (the one that only has "game") as a subfolder.
2. Copy the path
3. Run the backend again (step 3 for Windows, 7 for macOS), but append this (without the backticks, but **WITH** the quotes):
`--deadlock path "<path>"`
