// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using Moq;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Rooms;
using osu.Server.Spectator.Hubs;
using Xunit;

namespace osu.Server.Spectator.Tests.Multiplayer
{
    public class TeamVersusTests : MultiplayerTest
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task UserRequestsValidTeamChange(int team)
        {
            var hub = new Mock<IMultiplayerHubContext>();
            var room = new ServerMultiplayerRoom(1, hub.Object)
            {
                Playlist =
                {
                    new MultiplayerPlaylistItem
                    {
                        BeatmapID = 3333,
                        BeatmapChecksum = "3333"
                    },
                }
            };
            await room.Initialise(DatabaseFactory.Object);

            var teamVersus = new TeamVersus(room, hub.Object);

            // change the match type
            room.MatchTypeImplementation = teamVersus;

            var user = new MultiplayerRoomUser(1);

            room.AddUser(user);
            hub.Verify(h => h.NotifyMatchUserStateChanged(room, user), Times.Once());

            teamVersus.HandleUserRequest(user, new ChangeTeamRequest { TeamID = team });

            checkUserOnTeam(user, team);
            hub.Verify(h => h.NotifyMatchUserStateChanged(room, user), Times.Exactly(2));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task UserRequestsInvalidTeamChange(int team)
        {
            var hub = new Mock<IMultiplayerHubContext>();
            var room = new ServerMultiplayerRoom(1, hub.Object)
            {
                Playlist =
                {
                    new MultiplayerPlaylistItem
                    {
                        BeatmapID = 3333,
                        BeatmapChecksum = "3333"
                    },
                }
            };
            await room.Initialise(DatabaseFactory.Object);
            var teamVersus = new TeamVersus(room, hub.Object);

            // change the match type
            room.MatchTypeImplementation = teamVersus;

            var user = new MultiplayerRoomUser(1);

            room.AddUser(user);
            // called once on the initial user join operation (to inform other clients in the room).
            hub.Verify(h => h.NotifyMatchUserStateChanged(room, user), Times.Once());

            var previousTeam = ((TeamVersusUserState)user.MatchState!).TeamID;

            Assert.Throws<InvalidStateException>(() => teamVersus.HandleUserRequest(user, new ChangeTeamRequest { TeamID = team }));

            checkUserOnTeam(user, previousTeam);
            // was not called a second time from the invalid change.
            hub.Verify(h => h.NotifyMatchUserStateChanged(room, user), Times.Once());
        }

        [Fact]
        public async Task NewUsersAssignedToTeamWithFewerUsers()
        {
            var hub = new Mock<IMultiplayerHubContext>();
            var room = new ServerMultiplayerRoom(1, hub.Object)
            {
                Playlist =
                {
                    new MultiplayerPlaylistItem
                    {
                        BeatmapID = 3333,
                        BeatmapChecksum = "3333"
                    },
                }
            };
            await room.Initialise(DatabaseFactory.Object);

            // change the match type
            room.ChangeMatchType(MatchType.TeamVersus);

            // join a number of users initially to the room
            for (int i = 0; i < 5; i++)
                room.AddUser(new MultiplayerRoomUser(i));

            // change all users to team 0
            for (int i = 0; i < 5; i++)
                room.MatchTypeImplementation.HandleUserRequest(room.Users[i], new ChangeTeamRequest { TeamID = 0 });

            Assert.All(room.Users, u => checkUserOnTeam(u, 0));

            for (int i = 5; i < 10; i++)
            {
                var newUser = new MultiplayerRoomUser(i);

                room.AddUser(newUser);

                // all new users should be joined to team 1 to balance the user counts.
                checkUserOnTeam(newUser, 1);
            }
        }

        [Fact]
        public async Task InitialUsersAssignedToTeamsEqually()
        {
            var hub = new Mock<IMultiplayerHubContext>();
            var room = new ServerMultiplayerRoom(1, hub.Object)
            {
                Playlist =
                {
                    new MultiplayerPlaylistItem
                    {
                        BeatmapID = 3333,
                        BeatmapChecksum = "3333"
                    },
                }
            };
            await room.Initialise(DatabaseFactory.Object);

            // join a number of users initially to the room
            for (int i = 0; i < 5; i++)
                room.AddUser(new MultiplayerRoomUser(i));

            // change the match type
            room.ChangeMatchType(MatchType.TeamVersus);

            checkUserOnTeam(room.Users[0], 0);
            checkUserOnTeam(room.Users[1], 1);
            checkUserOnTeam(room.Users[2], 0);
            checkUserOnTeam(room.Users[3], 1);
            checkUserOnTeam(room.Users[4], 0);
        }

        [Fact]
        public async Task StateMaintainedBetweenRulesetSwitch()
        {
            var hub = new Mock<IMultiplayerHubContext>();
            var room = new ServerMultiplayerRoom(1, hub.Object)
            {
                Playlist =
                {
                    new MultiplayerPlaylistItem
                    {
                        BeatmapID = 3333,
                        BeatmapChecksum = "3333"
                    },
                }
            };
            await room.Initialise(DatabaseFactory.Object);

            room.ChangeMatchType(MatchType.TeamVersus);

            // join a number of users initially to the room
            for (int i = 0; i < 5; i++)
                room.AddUser(new MultiplayerRoomUser(i));

            checkUserOnTeam(room.Users[0], 0);
            checkUserOnTeam(room.Users[1], 1);
            checkUserOnTeam(room.Users[2], 0);
            checkUserOnTeam(room.Users[3], 1);
            checkUserOnTeam(room.Users[4], 0);

            // change the match type
            room.ChangeMatchType(MatchType.HeadToHead);
            room.ChangeMatchType(MatchType.TeamVersus);

            checkUserOnTeam(room.Users[0], 0);
            checkUserOnTeam(room.Users[1], 1);
            checkUserOnTeam(room.Users[2], 0);
            checkUserOnTeam(room.Users[3], 1);
            checkUserOnTeam(room.Users[4], 0);
        }

        private void checkUserOnTeam(MultiplayerRoomUser u, int team) =>
            Assert.Equal(team, (u.MatchState as TeamVersusUserState)?.TeamID);
    }
}
