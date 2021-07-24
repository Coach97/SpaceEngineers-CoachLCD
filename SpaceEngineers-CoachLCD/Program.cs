using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        struct TextSurfacePair
        {
            public IMyTextSurface surface;
            public IMyTerminalBlock terminal;
            public TextSurfacePair(IMyTextSurface surface, IMyTerminalBlock terminal)
            {
                this.surface = surface;
                this.terminal = terminal;
            }
        }

        struct CommandArgumentsPair
        {
            public string command;
            public string[] arguments;
            public CommandArgumentsPair(string command, string[] arguments)
            {
                this.command = command;
                this.arguments = arguments;
            }
        }

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {

        }

        List<TextSurfacePair> FindTextSurfaces()
        {
            List<TextSurfacePair> textSurfaces = new List<TextSurfacePair>();
            textSurfaces.Clear();
            List<IMyTerminalBlock> screens = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName("[LCD]", screens, e => e.CustomName.EndsWith("[LCD]"));
            screens.ForEach(screen =>
            {
                IMyTextSurfaceProvider provider = screen as IMyTextSurfaceProvider;
                IMyTextSurface surface = provider.GetSurface(0);
                surface.ContentType = ContentType.TEXT_AND_IMAGE;
                surface.Font = "Monospace";
                TextSurfacePair pair = new TextSurfacePair(surface, screen);
                textSurfaces.Add(pair);
            });
            return textSurfaces;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            List<TextSurfacePair> textSurfaces = this.FindTextSurfaces();
            textSurfaces.ForEach(textSurface =>
            {
                string data = textSurface.terminal.CustomData;
                List<CommandArgumentsPair> parsedData = this.ParseData(data);
                string output = ExecuteCommands(parsedData, textSurface);
                
                textSurface.surface.WriteText(output);
            });
        }

        string ExecuteCommands(List<CommandArgumentsPair>  commandArgsPairs, TextSurfacePair textSurface)
        {
            string output = "";
            foreach(CommandArgumentsPair commandArgPair in commandArgsPairs)
            {
                string commandOutput = ExecuteCommand(commandArgPair.command, commandArgPair.arguments, textSurface);
                if (commandOutput.Length > 0) output += commandOutput + "\n";
            }
            return output;
        }

        string ExecuteCommand(string command, string[] arguments, TextSurfacePair textSurface)
        {
            string firstArg = arguments.Length > 0 ? arguments[0] : "";
            int screenWidth = this.ScreenWidth(textSurface);
            switch (command)
            {
                // Settings
                case "Color":
                    if (arguments.Length != 3) return "Color: Expected args: \"<red>\" \"<green>\" \"<blue>\"";
                    try
                    {
                        textSurface.surface.FontColor = new Color(int.Parse(arguments[0]), int.Parse(arguments[1]), int.Parse(arguments[2]));
                    }catch(Exception e)
                    {
                        return "Error: Unable to parse arguments. Make sure they are integers";
                    }
                    return "";
                case "FontSize":
                    if (arguments.Length != 1) return "FontSize: Expected args: \"<font size>\"";
                    try
                    {
                        textSurface.surface.FontSize = float.Parse(firstArg);
                    }catch(Exception e)
                    {
                        return "Error: Unable to parse argument. Make sure it is a number";
                    }
                    return "";

                // Text rendering
                case "HLine":
                    return this.GenerateString('─', screenWidth);
                case "Echo":
                    return firstArg;
                case "Center":
                    return this.TextLayoutCenter(firstArg, textSurface);
                case "TwoCol":
                    if (arguments.Length != 2) return "TwoCol: Expected args: \"<left>\" \"<right>\"";
                    return this.TextLayoutTwoColumns(arguments, textSurface);

                // Property displays
                case "PropBool":
                    if (arguments.Length == 5)
                    {
                        return "";
                    }
                    else
                    {
                        try
                        {
                            IMyTerminalBlock block = GridTerminalSystem.GetBlockWithName(arguments[0]);
                            bool value = block.GetValueBool(arguments[1]);
                            string[] data = { arguments[2], (value ? arguments[3] : arguments[4]) };
                            return this.TextLayoutTwoColumns(data, textSurface);
                        }
                        catch (Exception e)
                        {
                            string[] data = { arguments[2], arguments[3] };
                            return this.TextLayoutTwoColumns(data, textSurface);
                        }

                    }
                case "Connected":
                    if (arguments.Length != 5) return "Connected: Expected args: \"<block>\" \"<text>\" \"<true text>\" \"<false text>\" \"<error text>\"";
                    try
                    {
                        IMyShipConnector connector = GridTerminalSystem.GetBlockWithName(arguments[0]) as IMyShipConnector;
                        bool connected = connector.Status == MyShipConnectorStatus.Connected;
                        string[] data = { arguments[1], (connected ? arguments[2] : arguments[3]) };
                        return this.TextLayoutTwoColumns(data, textSurface);
                    }
                    catch(Exception e)
                    {
                        string[] data = { arguments[1], arguments[4] };
                        return this.TextLayoutTwoColumns(data, textSurface);
                    }
                case "Extending":
                    if (arguments.Length != 5) return "Extending: Expected args: \"<block>\" \"<text>\" \"<true text>\" \"<false text>\" \"<error text>\"";
                    try
                    {
                        IMyPistonBase piston = GridTerminalSystem.GetBlockWithName(arguments[0]) as IMyPistonBase;
                        bool extending = piston.Status == PistonStatus.Extending;
                        string[] data = { arguments[1], (extending ? arguments[2] : arguments[3]) };
                        return this.TextLayoutTwoColumns(data, textSurface);
                    }
                    catch(Exception e)
                    {
                        string[] data = { arguments[1], arguments[4] };
                        return this.TextLayoutTwoColumns(data, textSurface);
                    }
                case "Retracting":
                    if (arguments.Length != 5) return "Retracting: Expected args: \"<block>\" \"<text>\" \"<true text>\" \"<false text>\" \"<error text>\"";
                    try
                    {
                        IMyPistonBase piston = GridTerminalSystem.GetBlockWithName(arguments[0]) as IMyPistonBase;
                        bool retracting = piston.Status == PistonStatus.Retracting;
                        string[] data = { arguments[1], (retracting ? arguments[2] : arguments[3]) };
                        return this.TextLayoutTwoColumns(data, textSurface);
                    }
                    catch (Exception e)
                    {
                        string[] data = { arguments[1], arguments[4] };
                        return this.TextLayoutTwoColumns(data, textSurface);
                    }
                case "PistonStatus":
                    if (arguments.Length != 6) return "PistonStatus: Expected args: \"<block>\" \"<text>\" \"<stopped text>\" \"<retracting text>\" \"<extending text>\" \"<error text>\"";
                    try
                    {
                        IMyPistonBase piston = GridTerminalSystem.GetBlockWithName(arguments[0]) as IMyPistonBase;
                        string statusText = arguments[2];
                        switch(piston.Status)
                        {
                            case PistonStatus.Retracting: statusText = arguments[3]; break;
                            case PistonStatus.Extending: statusText = arguments[4]; break;
                            default: statusText = arguments[2]; break;
                        }
                        string[] data = { arguments[1], statusText };
                        return this.TextLayoutTwoColumns(data, textSurface);

                    }catch(Exception e)
                    {
                        string[] data = { arguments[1], arguments[5] };
                        return this.TextLayoutTwoColumns(data, textSurface);
                    }
            }
            return "";
        }

        string JoinString(string[] stringarray, string join)
        {
            string output = stringarray.Aggregate("", (acc, value) => acc + value + join);
            if (output.Length > 0) output.Substring(0, output.Length - 1 - join.Length);
            return output;
        }

        string GenerateString(char character, int count) {
            string output = "";
            for (int i = 0; i < count; i++) output += character;
            return output;
        }

        int ScreenWidth(TextSurfacePair textSurface)
        {
            float basePanelWidth = 26;
            switch (textSurface.terminal.BlockDefinition.SubtypeId)
            {
                case "LargeLCDPanelWide": basePanelWidth = 53; break;
                case "LargeBlockCorner_LCD_1":
                case "LargeBlockCorner_LCD_2":
                case "LargeBlockCorner_LCD_Flat_1":
                case "LargeBlockCorner_LCD_Flat_2":
                    basePanelWidth = 106;
                    break;
            }
           // Echo($"{textSurface.terminal.BlockDefinition.SubtypeId}");
            int basePanelPadding = (int)(basePanelWidth * (textSurface.surface.TextPadding / 100f) * 2);
            return (int)((basePanelWidth - basePanelPadding) / textSurface.surface.FontSize);
        }

        string TextLayoutTwoColumns(string[] data, TextSurfacePair textSurface)
        {
            if (data.Length < 2) throw new Exception("TextLayoutTwoColumns requires an array with a length of at least 2");
            int width = this.ScreenWidth(textSurface);
            int halfWidth = (int)(width / 2);
            int leftWidth = Math.Min(halfWidth - 1, data[0].Length);
            int rightWidth = Math.Min(halfWidth - 1, data[1].Length);
            string output = data[0].Substring(0, leftWidth);
            output += GenerateString(' ', Math.Max(width - leftWidth - rightWidth, 1));
            output += data[1].Substring(0, rightWidth);
            return output;
        }

        string TextLayoutCenter(string text, TextSurfacePair textSurface)
        {
            int width = this.ScreenWidth(textSurface);
            int halfWidth = (int)(width / 2);
            int left = halfWidth - (text.Length / 2);

            string output = GenerateString(' ', left);
            output += text;
            return output;
        }

        List<CommandArgumentsPair> ParseData(string data)
        {
            List<CommandArgumentsPair> output = new List<CommandArgumentsPair>();
            string[] lines = data.Split('\n');
            foreach(string line in lines)
            {
                if (line.Length == 0) continue;
                string[] commandAndArguments = line.Split(' ');
                if (commandAndArguments.Length == 0 || commandAndArguments[0].Length == 0) continue;
                string command = commandAndArguments[0];
                bool first = true;
                string[] arguments = commandAndArguments.Aggregate(new List<string>(), (acc, value) =>
                {
                    if (first)
                    {
                        first = false;
                        return acc;
                    }
                    if (value.StartsWith("\"") && value.EndsWith("\"")) acc.Add(value.Substring(1, value.Length - 2));
                    else if (value.StartsWith("\"")) acc.Add(value.Substring(1));
                    else if (value.EndsWith("\"")) acc[acc.Count - 1] += " " + value.Substring(0, value.Length - 1);
                    else acc[acc.Count - 1] += " " + value;
                    return acc;
                }).ToArray();
                CommandArgumentsPair cmdArgPair = new CommandArgumentsPair(command, arguments);
                output.Add(cmdArgPair);
            }
            return output;
        }
    }
}
