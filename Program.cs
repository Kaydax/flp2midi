using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Monad.FLParser;
using MIDIModificationFramework;
using MIDIModificationFramework.Generator;
using MIDIModificationFramework.MIDIEvents;
using Note = MIDIModificationFramework.Note;
using System.Collections.Concurrent;

namespace flp2midi
{
  class Program
  {
    static bool ForceColor { get; set; }
    static bool DisableEcho { get; set; }

    //TODO: Actually support feed correctly
    static IEnumerable<Note> EchoNotes(IEnumerable<Note> notes, byte echoamount, uint feed, uint time, int ppq)
    {
      if(feed == 0) return notes;

      List<IEnumerable<Note>> echos = new List<IEnumerable<Note>>();

      echos.Add(notes);

      for(var i = 1; i <= echoamount; i++)
      {
        var shifted = notes.OffsetTime((time * i * ppq) / 96.0 / 2);
        echos.Add(shifted);
      }

      return echos.MergeAll();
    }

    static void Main(string[] args)
    {
      if(args.Length < 1 || !File.Exists(args[0]))
      {
        PrintHelp();
      }

      GetArgs(args);

      var filePath = args[0];
      var tempFile = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileName(filePath) + ".mid.tmp");
      File.Delete(tempFile); //Delete old temp file if it exist
      var streams = new ParallelStream(File.Open(tempFile, FileMode.Create));


      Console.WriteLine("flp2midi | Version: 1.2.1");
      Console.WriteLine("Loading FL Studio project file...");

      Project proj = Project.Load(filePath, false);

      string title = proj.ProjectTitle;
      string version = proj.VersionString;

      Console.WriteLine("Title: " + title + " | Version: " + version);

      object l = new object();

      var patternDict = new Dictionary<int, Dictionary<Channel, Note[]>>();

      Parallel.ForEach(proj.Patterns, pat =>
      {
        int id = pat.Id;
        string name = pat.Name;

        var notes = pat.Notes.ToDictionary(c => c.Key, c =>
        {
          byte channel = 0;
          var colorchan = false;

          if(c.Key.Data is GeneratorData data)
          {
            if(data.PluginSettings[29] == 0x01) colorchan = true;
            channel = data.PluginSettings[4];
          }

          var noteList = new List<Note>(c.Value.Count);

          var lastNoteZeroTick = -1.0;
          foreach(var n in c.Value.OrderBy(n => n.Position))
          {
            var newNote = new Note((colorchan || ForceColor) ? n.Color : channel, Math.Min((byte)127, n.Key), Math.Min((byte)127, n.Velocity), (double)n.Position, (double)n.Position + (double)n.Length);
            noteList.Add(newNote);

            if(lastNoteZeroTick != -1.0 && lastNoteZeroTick != newNote.Start)
            {
              lastNoteZeroTick = -1.0;
              noteList[^2].End = newNote.Start;
            }

            if(newNote.Length == 0)
            {
              lastNoteZeroTick = newNote.Start;
              newNote.End = double.PositiveInfinity;
            }
          }

          return noteList.ToArray();

          /*
          return c.Value
              .OrderBy(n => n.Position)
              .Select(n => new Note((colorchan || ForceColor) ? n.Color : channel, 
                                  Math.Min((byte)127, n.Key), 
                                  Math.Min((byte)127, n.Velocity), 
                                  (double)n.Position, 
                                  (double)n.Position + (double)n.Length))
              .ToArray();
          */
        });

        lock(l)
        {
          patternDict.Add(id, notes);
        }

        Console.WriteLine($"Pattern found: ({id}) {name}");
      });

      var trackID = 0;

      //MemoryStream[] streams = new MemoryStream[proj.Tracks.Length];

      var tracks = proj.Tracks.Where(t => t.Items.Count != 0).OrderBy(t =>
        t.Items.Select(i =>
        {
          var pi = i as PatternPlaylistItem;
          if(pi == null) return 0;
          return -patternDict[pi.Pattern.Id].Select(p => p.Value.Length).Sum();
        }).Sum()
      ).ToArray();

      //var automations = proj.Channels.Where(c => c.Data is AutomationData).ToArray();

      /*
          Parameters:
          1 - Unknown
          2 - Unknown
          3 - Unknown
          4 - Unknown
          5 - Tempo Events
      */

      /*
       * Modes:
       * 10 - Smooth
       * 2  - Hold
       * 0  - Single Curve
       * 7  - Single Curve 2
       * 11 - Single Curve 3
       * 1  - Double Curve
       * 8  - Double Curve 2
       * 12 - Double Curve 3
       * 9  - Half Sine
       * 3  - Stairs
       * 4  - Smooth Stairs
       * 5  - Pulse
       * 6  - Wave
      */

      //foreach (var auto in automations)
      //{
      //}

      ParallelFor(0, tracks.Length, Environment.ProcessorCount, new CancellationToken(false), i =>
      {
        //streams[i] = new MemoryStream();

        var stream = new BufferedStream(streams.GetStream(i), 1 << 24);
        var trackWriter = new MidiWriter(stream);

        var track = tracks[i];

        var notes = track.Items.Select(item =>
        {
          if(item is PatternPlaylistItem && item.Muted == false)
          {
            var pi = item as PatternPlaylistItem;
            var pattern = patternDict[pi.Pattern.Id];
            var merged = pattern.Select(c =>
            {
              var shifted = c.Value
                            .TrimStart(Math.Max(0, item.StartOffset))
                            .TrimEnd(Math.Max(0, item.EndOffset == -1 ? item.Length : item.EndOffset))
                            //.Where(n => n.Length > 0)
                            .OffsetTime(item.Position - item.StartOffset);

              var channel = c.Key;

              if(channel.Data is GeneratorData data)
              {
                if(data.EchoFeed > 0 && !DisableEcho)
                {
                  shifted = EchoNotes(shifted, data.Echo, data.EchoFeed, data.EchoTime, proj.Ppq);
                }
              }

              return shifted;
            }).MergeAll();

            return merged;
          }
          else
          {
            return new Note[0];
          }
        }).MergeAll().TrimStart();

        trackWriter.Write(notes.ExtractEvents());
        stream.Close();

        lock(l)
        {
          Console.WriteLine($"Generated track {i + 1}, {(trackID++) + 1}/{tracks.Length}");
        }
      });

      streams.CloseAllStreams();

      var writer = new MidiWriter(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileName(filePath) + ".mid"));
      writer.Init((ushort)proj.Ppq);
      writer.InitTrack();
      writer.Write(new TempoEvent(0, (int)(60000000.0 / proj.Tempo)));
      writer.EndTrack();

      for(int i = 0; i < tracks.Length; i++)
      {
        Console.WriteLine($"Writing track {i + 1}/{tracks.Length}");

        var stream = streams.GetStream(i, true);

        stream.Position = 0;
        unchecked
        {
          writer.WriteTrack(stream);
        }
        stream.Close();
      }

      writer.Close();
      streams.CloseAllStreams();
      streams.Dispose();
      File.Delete(tempFile);

      Console.WriteLine("Press any key to exit... ");
      Console.ReadKey();
    }

    //TODO: Abstract console vars
    static void GetArgs(string[] args)
    {
      for(var i = 0; i < args.Length; i++)
      {
        switch(args[i])
        {
          case "-fc":
          case "--forcecolor":
          {
            ForceColor = true;
            break;
          }
          case "-de":
          case "--disable-echo":
          {
            DisableEcho = true;
            break;
          }
          case "-h":
          case "--help":
          {
            PrintHelp();
            break;
          }
          default:
          {
            break;
          }
        }
      }
    }

    static void PrintHelp()
    {
      Console.WriteLine("Usage: flp2midi.exe <path to flp file>");
      Console.WriteLine("List of current console vars:" +
                        "\n-fc | --force-color > Makes all notes map to the color they are mapped to in the pattern than what the MIDI Out channel is set to" +
                        "\n-de | --disable-echo > Disables the generation of echo notes if echo is enabled on the channel");
      Console.WriteLine("Press any key to exit... ");
      Console.ReadKey();
      System.Environment.Exit(1);
    }

    static void ParallelFor(int from, int to, int threads, CancellationToken cancel, Action<int> func)
    {
      Dictionary<int, Task> tasks = new Dictionary<int, Task>();
      BlockingCollection<int> completed = new BlockingCollection<int>();

      void RunTask(int i)
      {
        var t = new Task(() =>
        {
          try
          {
            func(i);
          }
          finally
          {
            completed.Add(i);
          }
        });
        tasks.Add(i, t);
        t.Start();
      }

      void TryTake()
      {
        var t = completed.Take(cancel);
        tasks[t].Wait();
        tasks.Remove(t);
      }

      for(int i = from; i < to; i++)
      {
        RunTask(i);
        if(tasks.Count > threads) TryTake();
      }

      while(completed.Count > 0 || tasks.Count > 0) TryTake();
    }
  }
}
