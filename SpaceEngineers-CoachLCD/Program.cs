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
                List<CommandArgumentsPair> parsedData = this.ParseData(data, textSurface.terminal);
                string output = ExecuteCommands(parsedData, textSurface);
                
                textSurface.surface.WriteText(output);
            });
        }

        string ExecuteCommands(List<CommandArgumentsPair>  commandArgsPairs, TextSurfacePair textSurface)
        {
            string output = "";
            Dictionary<string, string> blockScriptData = new Dictionary<string, string>();
            foreach (CommandArgumentsPair commandArgPair in commandArgsPairs)
            {
                string commandOutput = ExecuteCommand(commandArgPair.command, commandArgPair.arguments, textSurface, blockScriptData);
                if (commandOutput.Length > 0) output += commandOutput + "\n";
            }
            return output;
        }

        string ExecuteCommand(string command, string[] preArguments, TextSurfacePair textSurface, Dictionary<string, string> data)
        {
            List<string> argList = new List<string>();
            foreach(string arg in preArguments)
            {
                string argWithData = arg;
                foreach(KeyValuePair<string, string> kv in data)
                {
                    Echo($"{kv.Key}, {argWithData}, {argWithData.Replace("${" + kv.Key + "}", kv.Value)}");
                    argWithData = argWithData.Replace("${" + kv.Key + "}", kv.Value);
                }
                argList.Add(argWithData);
            }
            string[] arguments = argList.ToArray();
            string firstArg = arguments.Length > 0 ? arguments[0] : "";
            int screenWidth = this.ScreenWidth(textSurface);
            switch (command)
            {
                // Settings
                case "Data":
                    if (arguments.Length != 2) return "Data: Expected args: \"<key>\" \"<value>\"";
                    try
                    {
                        data.Add(arguments[0], arguments[1]);
                        return "";
                    }catch(Exception e)
                    {
                        return "Data: " + e.Message;
                    }
                case "Color":
                    if (arguments.Length != 3) return "Color: Expected args: \"<red>\" \"<green>\" \"<blue>\"";
                    try
                    {
                        textSurface.surface.FontColor = new Color(int.Parse(arguments[0]), int.Parse(arguments[1]), int.Parse(arguments[2]));
                    }catch(Exception e)
                    {
                        return "Color: Unable to parse arguments. Make sure they are integers";
                    }
                    return "";
                case "FontSize":
                    if (arguments.Length != 1) return "FontSize: Expected args: \"<font size>\"";
                    try
                    {
                        textSurface.surface.FontSize = float.Parse(firstArg);
                    }catch(Exception e)
                    {
                        return "FontSize: Unable to parse argument. Make sure it is a number";
                    }
                    return "";

                // Text rendering
                case "HLine":
                    return this.GenerateString('─', screenWidth);
                case "Echo":
                    return firstArg == "" ? " " : firstArg;
                case "Center":
                    return this.TextLayoutCenter(firstArg, textSurface);
                case "TwoCol":
                    if (arguments.Length != 2) return "TwoCol: Expected args: \"<left>\" \"<right>\"";
                    return this.TextLayoutTwoColumns(arguments[0], arguments[1], textSurface);
                case "Col":
                    return this.TextLayoutEvenColumns(arguments, textSurface);

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
                            if (block.BlockDefinition.IsNull()) throw new Exception("Block not found");
                            bool value = block.GetValueBool(arguments[1]);
                            return this.TextLayoutTwoColumns(arguments[2], (value ? arguments[3] : arguments[4]), textSurface);
                        }
                        catch (Exception e)
                        {
                            return this.TextLayoutTwoColumns(arguments[2], arguments[3], textSurface);
                        }

                    }
                case "Connected":
                    if (arguments.Length != 5) return "Connected: Expected args: \"<block>\" \"<text>\" \"<true text>\" \"<false text>\" \"<error text>\"";
                    try
                    {
                        IMyShipConnector connector = GridTerminalSystem.GetBlockWithName(arguments[0]) as IMyShipConnector;
                        bool connected = connector.Status == MyShipConnectorStatus.Connected;
                        return this.TextLayoutTwoColumns(arguments[1], (connected ? arguments[2] : arguments[3]), textSurface);
                    }
                    catch(Exception e)
                    {
                        return this.TextLayoutTwoColumns(arguments[1], arguments[4], textSurface);
                    }
                case "Extending":
                    if (arguments.Length != 5) return "Extending: Expected args: \"<block>\" \"<text>\" \"<true text>\" \"<false text>\" \"<error text>\"";
                    try
                    {
                        IMyPistonBase piston = GridTerminalSystem.GetBlockWithName(arguments[0]) as IMyPistonBase;
                        bool extending = piston.Status == PistonStatus.Extending;
                        return this.TextLayoutTwoColumns(arguments[1], (extending ? arguments[2] : arguments[3]), textSurface);
                    }
                    catch(Exception e)
                    {
                        return this.TextLayoutTwoColumns(arguments[1], arguments[4], textSurface);
                    }
                case "Retracting":
                    if (arguments.Length != 5) return "Retracting: Expected args: \"<block>\" \"<text>\" \"<true text>\" \"<false text>\" \"<error text>\"";
                    try
                    {
                        IMyPistonBase piston = GridTerminalSystem.GetBlockWithName(arguments[0]) as IMyPistonBase;
                        bool retracting = piston.Status == PistonStatus.Retracting;
                        return this.TextLayoutTwoColumns(arguments[1], (retracting ? arguments[2] : arguments[3]), textSurface);
                    }
                    catch (Exception e)
                    {
                        return this.TextLayoutTwoColumns(arguments[1], arguments[4], textSurface);
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
                        return this.TextLayoutTwoColumns(arguments[1], statusText, textSurface);

                    }catch(Exception e)
                    {
                        return this.TextLayoutTwoColumns(arguments[1], arguments[5], textSurface);
                    }
                case "Cargo":
                    if (arguments.Length != 1) return "Cargo: Expected args: \"block\"";
                    try
                    {
                        IMyCargoContainer container = GridTerminalSystem.GetBlockWithName(arguments[0]) as IMyCargoContainer;
                        return this.PrintCargoContents(container.GetInventory(), textSurface);
                    }
                    catch(Exception e)
                    {
                        return "Cargo: " + e.Message;
                    }
                case "ConnectedCargo":
                    if (arguments.Length > 2 || arguments.Length < 1) return "Cargo: Expected args: \"<connector block>\" \"<?wide display>\"";
                    try
                    {
                        IMyShipConnector connector = GridTerminalSystem.GetBlockWithName(arguments[0]) as IMyShipConnector;
                        if (connector.Status != MyShipConnectorStatus.Connected) return "ConnectedCargo: Not Connected";
                        long gridId = connector.OtherConnector.CubeGrid.EntityId;
                        bool wide = arguments[1] == "true";
                        // Find blocks with inventorys which are in the other ship's grid
                        List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                        GridTerminalSystem.SearchBlocksOfName("", blocks, (block) =>
                        {
                            return block.HasInventory && block.CubeGrid.EntityId == gridId;
                        });
                        string output = "";
                        foreach(IMyTerminalBlock block in blocks)
                        {
                            output += this.PrintCargoContents(block.GetInventory(), textSurface, wide);
                        }
                        return output;

                    }
                    catch (Exception e)
                    {
                        return "ConnectedCargo: " + e.Message;
                    }
            }
            return "";
        }

        string ShorthandAmount(string amount, string unit = "")
        {
            int segmentLength = (int)Math.Floor(Math.Max(amount.Length - 1, 0) / 3f);
            string symbol = "";
            if (segmentLength == 1) symbol = "K";
            if (segmentLength == 2) symbol = "M";
            if (segmentLength == 3) symbol = "B";
            if (segmentLength == 4) symbol = "T";
            float floatAmt = float.Parse(amount);
            int len = segmentLength > 0 ? (int)Math.Pow(1000, segmentLength) : 1;
            string final = (floatAmt / len).ToString("#.##");
            return final + symbol + unit;
            
        }

        string PrintCargoContents(IMyInventory inventory, TextSurfacePair textSurface, bool wide = false)
        {
            List<MyInventoryItem> items = this.GetCargoContents(inventory);
            items.OrderBy(item => item.Type.SubtypeId.ToString());
            string output = "";
            List<string> group = new List<string>();
            foreach (MyInventoryItem item in items)
            {
                string unit = item.Type.TypeId.ToLower().Contains("ore") ? "g" : "";
                string amount = this.ShorthandAmount(item.Amount.ToString(), unit);
                string typeName = item.Type.TypeId.Replace("MyObjectBuilder_", "");
                if (typeName == "Component" || item.Type.SubtypeId == "Ice") typeName = "";
                string name = item.Type.SubtypeId + " " + typeName;
                if (wide)
                {
                    group.Add(name);
                    group.Add(amount);
                    if (group.Count == 4)
                    {
                        output += this.TextLayoutEvenColumns(group.ToArray(), textSurface) + "\n";
                        group.Clear();
                    }
                }else
                {
                    output += this.TextLayoutTwoColumns(name, amount, textSurface) + "\n";
                }
                
            }
            if (group.Count == 2)
            {
                group.Add("");
                group.Add("");
                output += this.TextLayoutEvenColumns(group.ToArray(), textSurface) + "\n";
            }
            return output;
        }

        List<MyInventoryItem> GetCargoContents(IMyInventory inventory)
        {
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            inventory.GetItems(items);
            return items;
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

        string TextLayoutTwoColumns(string left, string right, TextSurfacePair textSurface)
        {
            int width = this.ScreenWidth(textSurface);
            int halfWidth = (int)(width / 2f);
            int remainder = width - (halfWidth * 2);

            int leftWidth = Math.Min(halfWidth - 1, left.Length);
            int rightWidth = Math.Min(halfWidth - 1, right.Length);

            string output = left.Substring(0, Math.Min(halfWidth, leftWidth));
            output += GenerateString(' ', Math.Max(width - leftWidth - rightWidth, 2));
            output += right.Substring(0, Math.Min(halfWidth, rightWidth));
            return output;
        }

        string TextLayoutEvenColumns(string[] data, TextSurfacePair textSurface)
        {
            if (data.Length < 2) throw new Exception("TextLayoutTwoColumns requires an array with a length of at least 2");
            int width = this.ScreenWidth(textSurface);
            int colWidth = (int)(width / data.Length);
            int remainder = width - (colWidth * data.Length);
            int firstColGap = colWidth / 2;

            string output = "";
            bool isEven = data.Length % 2 == 0;
            for (int i = 0; i < data.Length; i++)
            {
                
                if (isEven)
                {
                    int currentColTextWidth = Math.Min(colWidth - 1, data[i].Length);
                    if (i % 2 == 0) output += data[i].Substring(0, currentColTextWidth) + GenerateString(' ', Math.Max(colWidth - currentColTextWidth, 1));
                    else
                    {
                        if (i == data.Length - 1) output += GenerateString(' ', Math.Max(remainder - 1, 0));
                        output += GenerateString(' ', Math.Max(colWidth - currentColTextWidth, 1)) + data[i].Substring(0, currentColTextWidth);
                        if (i != data.Length - 1) output += " ";
                    }
                }else {
                    int currentColTextWidth = Math.Min(colWidth - 1, data[i].Length);
                    if (i == data.Length - 1) output += GenerateString(' ', Math.Max(colWidth - currentColTextWidth, 1) + remainder);
                    output += data[i].Substring(0, currentColTextWidth);
                    if (i < data.Length - 1) output += GenerateString(' ', Math.Max(colWidth - currentColTextWidth, 1));
                }
            }
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

        List<CommandArgumentsPair> ParseData(string data, IMyTerminalBlock block)
        {
            List<CommandArgumentsPair> output = new List<CommandArgumentsPair>();
            string[] lines = data.Split('\n');
            foreach(string line in lines)
            {
                try
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
                        if (value.StartsWith("\"") && value.EndsWith("\"")) acc.Add(value.Substring(1, Math.Max(value.Length - 2, 0)));
                        else if (value.StartsWith("\"")) acc.Add(value.Substring(Math.Min(value.Length, 1)));
                        else if (value.EndsWith("\"")) acc[acc.Count - 1] += " " + value.Substring(0, Math.Max(value.Length - 1, 0));
                        else acc[acc.Count - 1] += " " + value;
                        return acc;
                    }).ToArray();
                    CommandArgumentsPair cmdArgPair = new CommandArgumentsPair(command, arguments);
                    output.Add(cmdArgPair);
                }
                catch(Exception e)
                {
                    Echo($"Error parsing custom data for block {block.CustomName}: " + e.Message);
                }
            }
            return output;
        }
    }
}
