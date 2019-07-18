using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Security;

namespace Microsoft.Bot.Builder.Adapters.Twilio
{
    public class TwilioAdapter : BotAdapter
    {
        public const string NAME = "Twilio SMS Adapter";

        private readonly ITwilioAdapterOptions options;

        public TwilioAdapter(ITwilioAdapterOptions options)
        {
            this.options = options;

            if (options.TwilioNumber.Equals(string.Empty))
            {
                throw new Exception("TwilioNumber is a required part of the configuration.");
            }

            if (options.AccountSID.Equals(string.Empty))
            {
                throw new Exception("AccountSID is a required part of the configuration.");
            }

            if (options.AuthToken.Equals(string.Empty))
            {
                throw new Exception("AuthToken is a required part of the configuration.");
            }

            TwilioClient.Init(this.options.AccountSID, this.options.AuthToken);
        }

        public TwilioBotWorker BotkitWorker { get; private set; }

        /// <summary>
        /// Standard BotBuilder adapter method to send a message from the bot to the messaging API.
        /// </summary>
        /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
        /// <param name="activities">An array of outgoing activities to be sent back to the messaging API.</param>
        /// <param name="cancellationToken">A cancellation token for the task.</param>
        /// <returns>A resource response.</returns>
        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, Activity[] activities, CancellationToken cancellationToken)
        {
            List<ResourceResponse> responses = new List<ResourceResponse>();
            for (var i = 0; i < activities.Length; i++)
            {
                var activity = activities[i];
                if (activity.Type == ActivityTypes.Message)
                {
                    var message = this.ActivityToTwilio(activity);

                    var res = await MessageResource.CreateAsync(message.To, message.AccountSid, message.From, message.MessagingServiceSid, message.Body, null, null, message.Sid);

                    var response = new ResourceResponse()
                    {
                        Id = res.Sid,
                    };

                    responses.Add(response);
                }
                else
                {
                    // log error: unknown message type
                }
            }

            return responses.ToArray();
        }

        /// <summary>
        /// Accept an incoming webhook request and convert it into a TurnContext which can be processed by the bot's logic.
        /// </summary>
        /// <param name="request">A request object from Restify or Express.</param>
        /// <param name="response">A response object from Restify or Express.</param>
        /// <param name="bot">A bot with logic function in the form `async(context) => { ... }`.</param>
        /// <param name="cancellationToken">A cancellation token for the task.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task ProcessAsync(HttpRequest request, HttpResponse response, IBot bot, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this.VerifySignature(request, response))
            {
                var bodyStream = new StreamReader(request.Body);
                MessageResource twilioEvent = JsonConvert.DeserializeObject(bodyStream.ReadToEnd()) as MessageResource;

                var activity = new Activity()
                {
                    Id = twilioEvent.MessagingServiceSid,
                    Timestamp = new DateTime(),
                    ChannelId = "twilio-sms",
                    Conversation = new ConversationAccount()
                    {
                        Id = twilioEvent.From.ToString(),
                    },
                    From = new ChannelAccount()
                    {
                        Id = twilioEvent.From.ToString(),
                    },
                    Recipient = new ChannelAccount()
                    {
                        Id = twilioEvent.To.ToString(),
                    },
                    Text = (twilioEvent as dynamic).Body,
                    ChannelData = twilioEvent,
                    Type = ActivityTypes.Message,
                };

                // Detect attachments
                if (Convert.ToInt32(twilioEvent.NumMedia) > 0)
                {
                    // specify a different event type for Botkit
                    (activity.ChannelData as dynamic).botkitEventType = "picture_message";
                }

                // create a conversation reference
                using (var context = new TurnContext(this, activity))
                {
                    await this.RunPipelineAsync(context, bot.OnTurnAsync, default(CancellationToken));

                    response.StatusCode = 200;
                    response.ContentType = "text/plain";
                    string text = (context.TurnState["httpBody"] != null) ? context.TurnState["httpBody"].ToString() : string.Empty;
                    await response.WriteAsync(text);
                }
            }
        }

        /// <summary>
        /// Standard BotBuilder adapter method to update a previous message with new content.
        /// </summary>
        /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
        /// <param name="activity">The updated activity in the form '{id: `id of activity to update`, ...}'.</param>
        /// <param name="cancellationToken">A cancellation token for the task.</param>
        /// <returns>A resource response with the Id of the updated activity.</returns>
        public override async Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, Activity activity, CancellationToken cancellationToken)
        {
            // Twilio adapter does not support updateActivity.
            return await Task.FromException<ResourceResponse>(new NotImplementedException("Twilio SMS does not support updating activities."));
        }

        /// <summary>
        /// Standard BotBuilder adapter method to delete a previous message.
        /// </summary>
        /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
        /// <param name="reference">An object in the form "{activityId: `id of message to delete`, conversation: { id: `id of channel`}}".</param>
        /// <param name="cancellationToken">A cancellation token for the task.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public override async Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            // Twilio adapter does not support deleteActivity.
            await Task.FromException<ResourceResponse>(new NotImplementedException("Twilio SMS does not support deleting activities."));
        }

        /// <summary>
        /// Formats a BotBuilder activity into an outgoing Twilio SMS message.
        /// </summary>
        /// <param name="activity">A BotBuilder Activity object.</param>
        /// <returns>a Twilio message object with {body, from, to, mediaUrl}.</returns>
        private MessageResource ActivityToTwilio(Activity activity)
        {
            List<Uri> mediaURLs = new List<Uri>();
            mediaURLs.Add(new Uri((activity.ChannelData as dynamic)?.mediaURL));
            MessageResource message = MessageResource.Create(activity.Conversation.Id, null, this.options.TwilioNumber, null, activity.Text, mediaURLs);

            return message;
        }

        /// <summary>
        /// Standard BotBuilder adapter method for continuing an existing conversation based on a conversation reference.
        /// </summary>
        /// <param name="reference">A conversation reference to be applied to future messages.</param>
        /// <param name="logic">A bot logic function that will perform continuing action in the form 'async(context) => { ... }'.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task ContinueConversationAsync(ConversationReference reference, BotCallbackHandler logic)
        {
            var request = reference.GetContinuationActivity().ApplyConversationReference(reference, true);

            using (var context = new TurnContext(this, request))
            {
                await this.RunPipelineAsync(context, logic, default(CancellationToken));
            }
        }

        /// <summary>
        /// Verify the signature of an incoming webhook request as originating from Twilio.
        /// </summary>
        /// <returns>If signature is valid, returns true. Otherwise, sends a 401 error status via http response and then returns false.</returns>
        private bool VerifySignature(HttpRequest request, HttpResponse response)
        {
            string twilioSignature;
            string validationURL;

            twilioSignature = request.Headers["x-twilio-signature"];

            validationURL = this.options.ValidationURL ?? (request.Headers["x-forwarded-proto"][0] ?? request.Protocol + "://" + request.Host + request.Path);

            var requestValidator = new RequestValidator(this.options.AuthToken);

            var bodyStream = new StreamReader(request.Body);
            dynamic payload = JsonConvert.DeserializeObject(bodyStream.ReadToEnd());
            string stringifiedBody = JsonConvert.SerializeObject(payload);

            if (!twilioSignature.Equals(string.Empty) && requestValidator.Validate(validationURL, stringifiedBody, twilioSignature))
            {
                return true;
            }

            // log error: Signature verification failed, Ignoring message

            return false;
        }
    }
}
