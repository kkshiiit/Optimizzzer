using System;
using System.Linq;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;
using Xunit;

namespace Optimizzzer.Tests
{
    public class GameSessionTests
    {
        [Fact]
        public void StartAndStopRestoreEverything()
        {
            var sys = new FakeSystem();
            sys.Registry.SetValue(Hive.LocalMachine, GameSession.WuKey, "PauseUpdatesExpiryTime", RegValue.Str("2020-01-01T00:00:00Z"));
            var before = sys.Process.ActiveScheme;
            var now = new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);

            var s = GameSession.Start(sys.Context, new[] { "OneDrive.exe", "notepad.exe" }, now);

            Assert.Equal(GameSession.HighPerformance, sys.Process.ActiveScheme);
            Assert.Equal("2026-11-21T18:00:00Z", sys.Registry.GetValue(Hive.LocalMachine, GameSession.WuKey, "PauseUpdatesExpiryTime").Data);
            Assert.Equal(new[] { "OneDrive.exe" }, s.Closed);
            Assert.Single(sys.Process.Killed);

            var again = GameSessionState.Parse(s.Serialize());
            GameSession.Stop(sys.Context, again);

            Assert.Equal(before, sys.Process.ActiveScheme);
            Assert.Equal("2020-01-01T00:00:00Z", sys.Registry.GetValue(Hive.LocalMachine, GameSession.WuKey, "PauseUpdatesExpiryTime").Data);
            Assert.Null(sys.Registry.GetValue(Hive.LocalMachine, GameSession.WuKey, "PauseUpdatesStartTime"));
        }

        [Fact]
        public void RunningListsOnlyKnownApps()
        {
            var sys = new FakeSystem();
            sys.Process.TaskList = "\"System\",\"4\",\"Services\",\"0\",\"1 K\"\n\"Spotify.exe\",\"10\",\"Console\",\"1\",\"9 K\"\n\"spotify.exe\",\"11\",\"Console\",\"1\",\"9 K\"";
            var r = GameSession.Running(sys.Context);
            Assert.Equal(new[] { "Spotify" }, r.Select(a => a.Name));
        }

        [Fact]
        public void ParseRejectsGarbage()
        {
            Assert.Null(GameSessionState.Parse(""));
            Assert.Null(GameSessionState.Parse("hello"));
        }
    }
}
