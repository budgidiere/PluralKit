using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using App.Metrics;

using Autofac;

using DSharpPlus;
using DSharpPlus.Entities;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Context
    {
        private ILifetimeScope _provider;

        private readonly DiscordRestClient _rest;
        private readonly DiscordShardedClient _client;
        private readonly DiscordClient _shard;
        private readonly DiscordMessage _message;
        private readonly Parameters _parameters;
        private readonly MessageContext _messageContext;

        private readonly IDataStore _data;
        private readonly PKSystem _senderSystem;
        private readonly IMetrics _metrics;

        private Command _currentCommand;

        public Context(ILifetimeScope provider, DiscordClient shard, DiscordMessage message, int commandParseOffset,
                       PKSystem senderSystem, MessageContext messageContext)
        {
            _rest = provider.Resolve<DiscordRestClient>();
            _client = provider.Resolve<DiscordShardedClient>();
            _message = message;
            _shard = shard;
            _data = provider.Resolve<IDataStore>();
            _senderSystem = senderSystem;
            _messageContext = messageContext;
            _metrics = provider.Resolve<IMetrics>();
            _provider = provider;
            _parameters = new Parameters(message.Content.Substring(commandParseOffset));
        }

        public DiscordUser Author => _message.Author;
        public DiscordChannel Channel => _message.Channel;
        public DiscordMessage Message => _message;
        public DiscordGuild Guild => _message.Channel.Guild;
        public DiscordClient Shard => _shard;
        public DiscordShardedClient Client => _client;
        public MessageContext MessageContext => _messageContext;

        public DiscordRestClient Rest => _rest;

        public PKSystem System => _senderSystem;
        
        public Parameters Parameters => _parameters;

        // TODO: this is just here so the extension methods can access it; should it be public/private/?
        internal IDataStore DataStore => _data;

        public Task<DiscordMessage> Reply(string text = null, DiscordEmbed embed = null, IEnumerable<IMention> mentions = null)
        {
            if (!this.BotHasAllPermissions(Permissions.SendMessages))
                // Will be "swallowed" during the error handler anyway, this message is never shown.
                throw new PKError("PluralKit does not have permission to send messages in this channel.");

            if (embed != null && !this.BotHasAllPermissions(Permissions.EmbedLinks))
                throw new PKError("PluralKit does not have permission to send embeds in this channel. Please ensure I have the **Embed Links** permission enabled.");
            return Channel.SendMessageFixedAsync(text, embed: embed, mentions: mentions);
        }
        
        public async Task Execute<T>(Command commandDef, Func<T, Task> handler)
        {
            _currentCommand = commandDef;

            try
            {
                await handler(_provider.Resolve<T>());
                _metrics.Measure.Meter.Mark(BotMetrics.CommandsRun);
            }
            catch (PKSyntaxError e)
            {
                await Reply($"{Emojis.Error} {e.Message}\n**Command usage:**\n> pk;{commandDef.Usage}");
            }
            catch (PKError e)
            {
                await Reply($"{Emojis.Error} {e.Message}");
            }
            catch (TimeoutException)
            {
                // Got a complaint the old error was a bit too patronizing. Hopefully this is better?
                await Reply($"{Emojis.Error} Operation timed out, sorry. Try again, perhaps?");
            }
        }

        public LookupContext LookupContextFor(PKSystem target) => 
            System?.Id == target.Id ? LookupContext.ByOwner : LookupContext.ByNonOwner;
        
        public LookupContext LookupContextFor(SystemId systemId) => 
            System?.Id == systemId ? LookupContext.ByOwner : LookupContext.ByNonOwner;

        public LookupContext LookupContextFor(PKMember target) =>
            System?.Id == target.System ? LookupContext.ByOwner : LookupContext.ByNonOwner;
        
        public IComponentContext Services => _provider;
    }
}