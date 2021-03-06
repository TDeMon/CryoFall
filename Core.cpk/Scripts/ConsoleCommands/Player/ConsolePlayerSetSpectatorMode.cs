﻿// ReSharper disable CanExtractXamlLocalizableStringCSharp

namespace AtomicTorch.CBND.CoreMod.ConsoleCommands.Player
{
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.Systems.Console;
    using AtomicTorch.CBND.GameApi.Data.Characters;

    public class ConsolePlayerSetSpectatorMode : BaseConsoleCommand
    {
        public override string Alias => "spectator";

        public override string Description
            => "Toggles spectator mode.";

        // this command is available for operators and moderators
        public override ConsoleCommandKinds Kind => ConsoleCommandKinds.ServerModerator;

        public override string Name => "player.setSpectatorMode";

        public string Execute(bool isEnabled, [CurrentCharacterIfNull] ICharacter character)
        {
            if (isEnabled)
            {
                PlayerCharacterSpectator.ServerSwitchToSpectatorMode(character);
            }
            else
            {
                PlayerCharacterSpectator.ServerSwitchToPlayerMode(character);
            }

            return null;
        }
    }
}