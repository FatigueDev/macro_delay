﻿using System.Linq.Expressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace macro_delay;

public class MacroDelay : ModSystem
{
    private static ICoreClientAPI? capi;
    private static Dictionary<int, List<long>> listenerIds = new Dictionary<int, List<long>>();

    public override bool ShouldLoad(EnumAppSide side)
    {
        return side == EnumAppSide.Client;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;

        api.ChatCommands.Create("mdrun")
            .WithDescription("Run a macro and delay output when we read `.mdelay (TimeInMilliseconds)` from it.")
            .WithArgs(new CommandArgumentParsers(api).Int("Macro Index (starts at 0)"), new CommandArgumentParsers(api).OptionalInt("Global delay in milliseconds", 0))
            .HandleWith((args) => {
                return RunDelayMacro((int)args[0], (int)args[1]);
            });

        api.ChatCommands.Create("mdstop")
            .WithDescription("Stops all or selected currently running delayed macros.")
            .WithArgs(new CommandArgumentParsers(api).OptionalInt("Macro Index (starts at 0)", -1))
            .HandleWith((args) => {
                return StopDelayMacro((int)args[0]);
            });

        api.ChatCommands.Create("mdlist")
            .WithDescription("List the keys of currently running (or unescaped) macros.")
            .HandleWith((args) =>
            {
                foreach(long key in listenerIds.Select((e) => e.Key))
                {
                    api.ShowChatMessage($"There's a macro running with they index {key}");
                }
                return TextCommandResult.Success(string.Empty);
            });
    }

    public static TextCommandResult RunDelayMacro(int macroIndex, int globalDelay)
    {
        SortedDictionary<int, IMacroBase> macros = capi.MacroManager.MacrosByIndex;

        if(!macros.ContainsKey(macroIndex)) return TextCommandResult.Error("The index of the macro given was not in your list of macros.");

        IMacroBase macro = macros.ElementAt(macroIndex).Value;

        if(listenerIds.ContainsKey(macroIndex))
        {
            UnregisterEventCallbacks((ICoreClientAPI)capi, macroIndex);
        }
        
        RegisterEventCallbacks(macro, (ICoreClientAPI)capi, globalDelay);

        return TextCommandResult.Success($"Running macro {macro.Name} with delays.");
    }

    public static TextCommandResult StopDelayMacro(int macroIndex)
    {
        if(macroIndex == -1)
        {
            UnregisterEventCallbacks(capi);
        }
        else
        {
            UnregisterEventCallbacks(capi, macroIndex);
        }

        return TextCommandResult.Success("Stopping selected delay macros.");
    }

    public static void UnregisterEventCallbacks(ICoreClientAPI api)
    {
        foreach(int key in listenerIds.Keys)
        {
            UnregisterEventCallbacks(api, key);
        }
        
        listenerIds.Clear();
    }

    public static void UnregisterEventCallbacks(ICoreClientAPI api, int listenerKey)
    {
        if(listenerIds.ContainsKey(listenerKey))
        {
            foreach(List<long> ids in listenerIds.Where((element) => element.Key == listenerKey).Select((element) => element.Value).AsEnumerable())
            {
                foreach(long id in ids)
                {
                    api.Event.UnregisterCallback(id);
                }
            }
            listenerIds.Remove(listenerKey);
        }
    }

    public static void RegisterEventCallbacks(IMacroBase macro, ICoreClientAPI api, int globalDelay = 0)
    {
        int totalms = 0;
        List<long> newIds = new List<long>();

        foreach(string command in macro.Commands)
        {            
            int commandDelay = 0;
            bool isCommandDelay = false;
            bool isCommandRepeat = false;
            int? macroToStart = null;
            int? macroToStartGlobalDelay = null;
            int? macroToStop = null;

            if(command.StartsWith(".mdelay ")) {
                var split = command.Split(" ").AsEnumerable();
                if(split.Count() == 2) {
                    if(int.TryParse(split.Last().ToString(), out commandDelay) == false)
                    {
                        api.ShowChatMessage($"There was an error parsing one of your delays. We're gonna stop all macros. Please re-check your macro for any malformed .delay calls. The command was: {command}");
                        UnregisterEventCallbacks(api);
                        return;
                    }
                    else
                    {
                        isCommandDelay = true;
                    }
                }
            }

            if(command.Equals(".mdrepeat")) {
                isCommandRepeat = true;
            }

            if(command.StartsWith(".mdrun ")) {
                var split = command.Split(" ").AsEnumerable();
                
                if(split.Count() == 2)
                {
                    if(split.ElementAtOrDefault(1) != null) {
                        if((macroToStart = ToNullableInt(split.ElementAt(1))) == null)
                        {
                            api.ShowChatMessage($"There was an error parsing an .mdrun command. We're gonna stop all macros. Please re-check your macro for any malformed .delay calls. The command was: {command}");
                            UnregisterEventCallbacks(api);
                            return;
                        }
                    }
                }

                if(split.Count() == 3)
                {
                    if(split.ElementAtOrDefault(1) != null) {
                        if((macroToStart = ToNullableInt(split.ElementAt(1))) == null)
                        {
                            api.ShowChatMessage($"There was an error parsing an .mdrun command. We're gonna stop all macros. Please re-check your macro for any malformed .delay calls. The command was: {command}");
                            UnregisterEventCallbacks(api);
                            return;
                        }
                    }

                    if(split.ElementAtOrDefault(2) != null)
                    {
                        if((macroToStartGlobalDelay = ToNullableInt(split.ElementAt(2))) == null)
                        {
                            api.ShowChatMessage($"There was an error parsing an .mdrun command. We're gonna stop all macros. Please re-check your macro for any malformed .delay calls. The command was: {command}");
                            UnregisterEventCallbacks(api);
                            return;
                        }
                    }

                }
                
            }

            if(command.StartsWith(".mdstop ")) {
                var split = command.Split(" ").AsEnumerable();
                if(split.Count() == 2) {
                    if((macroToStop = ToNullableInt(split.Last().ToString())) == null)
                    {
                        api.ShowChatMessage($"There was an error parsing an .mdstop command. We're gonna stop all macros anyway, though. Please re-check your macro for any malformed .delay calls. The command was: {command}");
                        UnregisterEventCallbacks(api);
                        return;
                    }
                }
            }

            newIds.Add(api.Event.RegisterCallback((dt) => {
                if(!isCommandDelay && !isCommandRepeat) {
                    api.SendChatMessage(command, GlobalConstants.CurrentChatGroup);
                }

                if(macroToStop != null) {
                    StopDelayMacro((int)macroToStop);
                }

                if(macroToStart != null) {
                    int delay = macroToStartGlobalDelay == null ? 0 : (int)macroToStartGlobalDelay;
                    RunDelayMacro((int)macroToStart, delay);
                }

                if(isCommandRepeat) {
                    RunDelayMacro(macro.Index, globalDelay);
                }
            }, totalms));

            totalms += isCommandDelay ? (commandDelay + globalDelay) : globalDelay;
        }
        
        listenerIds.Add(macro.Index, newIds);
    }

    public static int? ToNullableInt(string s)
    {
        int i;
        if (int.TryParse(s, out i)) return i;
        return null;
    }
}