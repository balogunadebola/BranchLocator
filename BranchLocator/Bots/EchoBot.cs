// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.22.0

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace BranchLocator.Bots
{
    public class EchoBot : ActivityHandler
    {

        private readonly Dialog _dialog;
        private readonly ConversationState _conversationState;
        private readonly IStatePropertyAccessor<DialogState> _dialogStateAccessor;
        public EchoBot(BranchLocatorDialog dialog, ConversationState conversationState)
        {
            _dialog = dialog;
            _conversationState = conversationState;
            
            // Initialize dialog state accessor
            _dialogStateAccessor = _conversationState.CreateProperty<DialogState>("DialogState");
        }
        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            // Save the conversation state
            await base.OnTurnAsync(turnContext, cancellationToken);
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        }
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            /*var replyText = $"Echo: {turnContext.Activity.Text}";
            await turnContext.SendActivityAsync(MessageFactory.Text(replyText, replyText), cancellationToken);*/

            // Create a DialogContext
            var dialogSet = new DialogSet(_dialogStateAccessor);
            dialogSet.Add(_dialog);

            var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken);

            // Ensure the dialog is not already active
            if (dialogContext.ActiveDialog == null)
            {
                // Start the dialog
                await dialogContext.BeginDialogAsync(_dialog.Id, null, cancellationToken);
            }
            else
            {
                // Continue the existing dialog
                await dialogContext.ContinueDialogAsync(cancellationToken);
            }

            // Run the dialog
            //var dialogState = _conversationState.CreateProperty<DialogState>("DialogState");
            //await _dialog.BeginDialogAsync(_dialog.Id, null, cancellationToken);

            // Save the conversation state
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            //var welcomeText = "Hello and welcome!";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    //await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                    await turnContext.SendActivityAsync(
                MessageFactory.Text("Welcome! I'm here to assist you."),
                cancellationToken);
                    // Create a DialogContext to start the dialog
                    var dialogSet = new DialogSet(_dialogStateAccessor);
                    dialogSet.Add(_dialog);

                    var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken);

                    // Start the dialog immediately
                    await dialogContext.BeginDialogAsync(_dialog.Id, null, cancellationToken);

                    // Save the conversation state
                    await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);

                }
            }
        }
    }
}
