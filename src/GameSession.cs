using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Helox {
public sealed class GameSessionReport {
    public string[] RunningGames {get;set;}
    public bool QuerySucceeded {get;set;}
    public string Error {get;set;}
    public string Source {get;set;}
}
internal static class GameSession {
    internal static GameSessionReport Read(Func<string,bool> running=null) {
        if(running==null) running=delegate(string name) {
            Process[] processes=Process.GetProcessesByName(name);try {return processes.Length>0;}finally {foreach(Process p in processes) p.Dispose();}
        };
        List<string> games=new List<string>();string error=null;
        foreach(string game in new string[]{"valorant","cs2","kovaaks"}) {
            string[] names=game=="valorant" ? new string[]{"VALORANT","VALORANT-Win64-Shipping"} : game=="cs2" ? new string[]{"cs2"} : new string[]{"FPSAimTrainer","FPSAimTrainer-Win64-Shipping"};
            bool active=false;foreach(string name in names) try {if(running(name)) active=true;}catch(Exception e) {error=e.Message;}
            if(active) games.Add(game);
        }
        return new GameSessionReport {RunningGames=games.ToArray(),QuerySucceeded=error==null,Error=error,Source="supported process names only / no game memory, files, input path or anticheat approval measured"};
    }
}
}
