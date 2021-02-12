# flp2midi
flp2midi was made because of the fact the built in exporter for midis in FL Studio is both slow and flawed. flp2midi was made for the purpose to export black midis made in FL Studio into fully working midi files as fast as possible. flp2midi can export files that take up to an hour in a few minutes depending on how many notes the file has.

> Currently tested FL Studio versions: 20.6+ (Older versions are untested, and may cause issues when exporting)
> Best way to fix this is to open the older project in a newer FL Studio version and save it

##### Current issues:
- flp2midi was designed for use with the MIDI Out vst. Other vst's may cause issues when exporting if used
- Automation clips are currently not supported. If you use automation clips, please export them seperately and merge the midi from flp2midi with the automation clips midi using [SAFC](https://github.com/DixelU/SAFC)
- Edit Events will not be supported as it's currently undocumented on how they work, making it hard to parse

## How to use
(flp2midi requires .net 5.0+ in order to run, you can get the desktop runtime [here](https://dotnet.microsoft.com/download/dotnet/5.0))

- First download the release archive and extract it to where ever you want.
- Drag and drop the flp you want to extract onto the exe and a console should appear
- Wait for the program to say its finished and a midi file with the same name as your flp should appear right next to it

------------

It's that simple. If you have any errors please make an issue on this repo.