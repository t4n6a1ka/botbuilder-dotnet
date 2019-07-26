﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Input;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Recognizers;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Rules;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Steps;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Dialogs.Declarative;
using Microsoft.Bot.Builder.Dialogs.Declarative.Resources;
using Microsoft.Bot.Builder.Dialogs.Declarative.Types;
using Microsoft.Bot.Builder.Expressions;
using Microsoft.Bot.Builder.Expressions.Parser;
using Microsoft.Bot.Builder.LanguageGeneration;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Bot.Builder.Dialogs.Adaptive.Tests
{
    [TestClass]
    public class StepsTests
    {
        public TestContext TestContext { get; set; }

        private TestFlow CreateFlow(AdaptiveDialog testDialog, bool sendTrace = false)
        {
            TypeFactory.Configuration = new ConfigurationBuilder().Build();
            var storage = new MemoryStorage();
            var convoState = new ConversationState(storage);
            var userState = new UserState(storage);
            var resourceExplorer = new ResourceExplorer();
            var adapter = new TestAdapter(TestAdapter.CreateConversation(TestContext.TestName), sendTrace);
            adapter
                .UseStorage(storage)
                .UseState(userState, convoState)
                .UseResourceExplorer(resourceExplorer)
                .UseLanguageGeneration(resourceExplorer)
                .Use(new TranscriptLoggerMiddleware(new FileTranscriptLogger()));
            DialogManager dm = new DialogManager(testDialog);

            return new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await dm.OnTurnAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
            });
        }


        [TestMethod]
        public async Task Step_WaitForInput()
        {
            var testDialog = new AdaptiveDialog("planningTest");

            testDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new TextInput() { Prompt = new ActivityTemplate("Hello, what is your name?"),  OutputBinding = "user.name" },
                        new SendActivity("Hello {user.name}, nice to meet you!"),
                    })});

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Hello, what is your name?")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
            .StartTestAsync();
        }

#if needsmoq
        [TestMethod]
        public async Task Step_HttpRequest()
        {
            var testDialog = new AdaptiveDialog("planningTest");

            testDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new HttpRequest()
                        {
                            Url = "https://translatorbotfn.azurewebsites.net/api/TranslateMessage?code=DFgQcuVqFkvP0nL27sibggKfMGYM8/fjNCZWywCwR1mbXclw0uSk3A==",
                            Method = HttpRequest.HttpMethod.POST,
                            ResponseType = HttpRequest.ResponseTypes.Activity,
                            Body = JObject.FromObject(new
                                {
                                    text = "Azure",
                                    fromLocale ="english",
                                    toLocale = "italian"
                                })
                        },
                    })
            });

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply(reply =>
                {
                    var message = reply.AsMessageActivity();
                    Assert.AreEqual(1, message.Attachments.Count);
                })
            .StartTestAsync();
        }
#endif
        [TestMethod]
        public async Task Step_TraceActivity()
        {
            var dialog = new AdaptiveDialog("traceActivity");

            dialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new SetProperty()
                        {
                             Property = "user.name",
                             Value = "'frank'"
                        },
                        new TraceActivity()
                        {
                            Name = "test",
                            ValueType = "user",
                            Value = "user"
                        },
                        new TraceActivity()
                        {
                            Name = "test",
                            ValueType = "memory"
                        }
                    })});

            await CreateFlow(dialog, sendTrace: true)
            .Send("hi")
                .AssertReply((activity) =>
                {
                    var traceActivity = (ITraceActivity)activity;
                    Assert.AreEqual(ActivityTypes.Trace, traceActivity.Type, "type doesn't match");
                    Assert.AreEqual("user", traceActivity.ValueType, "ValueType doesn't match");
                    Assert.AreEqual("frank", (string)((dynamic)traceActivity.Value).name, "Value doesn't match");
                })
                .AssertReply((activity) =>
                {
                    var traceActivity = (ITraceActivity)activity;
                    Assert.AreEqual(ActivityTypes.Trace, traceActivity.Type, "type doesn't match");
                    Assert.AreEqual("memory", traceActivity.ValueType, "ValueType doesn't match");
                    Assert.AreEqual("frank", (string)((dynamic)traceActivity.Value).user.name, "Value doesn't match");
                })
                .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_IfCondition()
        {
            var testDialog = new AdaptiveDialog("planningTest");
            testDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new IfCondition()
                        {
                            Condition = "user.name == null",
                            Steps = new List<IDialog>()
                            {
                                new TextInput() {
                                    Prompt  = new ActivityTemplate("Hello, what is your name?"),
                                    OutputBinding = "user.name"
                                },
                                new SendActivity("Hello {user.name}, nice to meet you!")
                            },
                            ElseSteps = new List<IDialog>()
                            {
                                new SendActivity("Hello {user.name}, nice to see you again!")
                            }
                        },
                    })});

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Hello, what is your name?")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
            .Send("hi")
                .AssertReply("Hello Carlos, nice to see you again!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_Switch()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                Steps = new List<IDialog>()
                    {
                        new SetProperty()
                        {
                            Property = "user.name",
                            Value = "'frank'"
                        },
                        new SwitchCondition()
                        {
                            Condition = "user.name",
                            Cases = new List<Case>()
                            {
                                new Case("susan", new List<IDialog>() { new SendActivity("hi susan") } ),
                                new Case("bob", new List<IDialog>() { new SendActivity("hi bob") } ),
                                new Case("frank", new List<IDialog>() { new SendActivity("hi frank") } )
                            },
                            Default = new List<IDialog>() { new SendActivity("Who are you?") }
                        },
                    }
            };

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("hi frank")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_Switch_Default()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                Steps = new List<IDialog>()
                    {
                        new SetProperty()
                        {
                            Property = "user.name",
                            Value = "'Zoidberg'"
                        },
                        new SwitchCondition()
                        {
                            Condition = "user.name",
                            Cases = new List<Case>()
                            {
                                new Case("susan", new List<IDialog>() { new SendActivity("hi susan") } ),
                                new Case("bob", new List<IDialog>() { new SendActivity("hi bob") } ),
                                new Case("frank", new List<IDialog>() { new SendActivity("hi frank") } )
                            },
                            Default = new List<IDialog>() { new SendActivity("Who are you?") }
                        },
                    }
            };

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Who are you?")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_Switch_Number()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                Steps = new List<IDialog>()
                    {
                        new SetProperty()
                        {
                            Property = "user.age",
                            Value = "22"
                        },
                        new SwitchCondition()
                        {
                            Condition = "user.age",
                            Cases = new List<Case>()
                            {
                                new Case("21", new List<IDialog>() { new SendActivity("Age is 21") } ),
                                new Case("22", new List<IDialog>() { new SendActivity("Age is 22") } ),
                                new Case("23", new List<IDialog>() { new SendActivity("Age is 23") } )
                            },
                            Default = new List<IDialog>() { new SendActivity("Who are you?") }
                        },
                    }
            };

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Age is 22")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_Switch_Bool()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                Steps = new List<IDialog>()
                    {
                        new SetProperty()
                        {
                            Property = "user.isVip",
                            Value = "true"
                        },
                        new SwitchCondition()
                        {
                            Condition = "user.isVip",
                            Cases = new List<Case>()
                            {
                                new Case("True", new List<IDialog>() { new SendActivity("User is VIP") } ),
                                new Case("False", new List<IDialog>() { new SendActivity("User is NOT VIP") } )
                            },
                            Default = new List<IDialog>() { new SendActivity("Who are you?") }
                        },
                    }
            };

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("User is VIP")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_TextInput()
        {
            var testDialog = new AdaptiveDialog("planningTest");

            testDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new IfCondition()
                        {
                            Condition = "user.name == null",
                            Steps = new List<IDialog>()
                            {
                                new TextInput()
                                {
                                    Prompt = new ActivityTemplate("Hello, what is your name?"),
                                    UnrecognizedPrompt = new ActivityTemplate("How should I call you?"),
                                    Property = "user.name",
                                    Validations = new List<string>()
                                    {
                                        "turn.value.Length > 3"
                                    }
                                }
                            }
                        },
                        new SendActivity("Hello {user.name}, nice to meet you!")
                    })});

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Hello, what is your name?")
            .Send("c")
                .AssertReply("How should I call you?")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_NumberInputWithDefaultValue()
        {
            var testDialog = new AdaptiveDialog("planningTest");

            testDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule
                {
                    Steps = new List<IDialog>()
                    {
                        new NumberInput() {
                            MaxTurnCount = 1,
                            DefaultValue = "10",
                            Prompt = new ActivityTemplate("What is your age?"),
                            Property = "turn.age"
                        },
                        new SendActivity("You said {turn.age}")
                    }
                }
            });

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("What is your age?")
            .Send("hi")
                .AssertReply("You said 10")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_ConfirmInput()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                AutoEndDialog = false,
                Steps = new List<IDialog>()
                {
                    new ConfirmInput()
                    {
                        Prompt = new ActivityTemplate("yes or no"),
                        UnrecognizedPrompt = new ActivityTemplate("I need a yes or no."),
                        Property = "user.confirmed"
                    },
                    new SendActivity("confirmation: {user.confirmed}"),
                    new ConfirmInput()
                    {
                        Prompt = new ActivityTemplate("yes or no"),
                        UnrecognizedPrompt = new ActivityTemplate("I need a yes or no."),
                        Property = "user.confirmed",
                        AlwaysPrompt = true
                    },
                    new SendActivity("confirmation: {user.confirmed}"),
                    new ConfirmInput()
                    {
                        Prompt = new ActivityTemplate("yes or no"),
                        UnrecognizedPrompt = new ActivityTemplate("I need a yes or no."),
                        Property = "user.confirmed",
                        AlwaysPrompt = true
                    },
                    new SendActivity("confirmation: {user.confirmed}"),
                }
            };

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("yes or no (1) Yes or (2) No")
            .Send("asdasd")
                .AssertReply("I need a yes or no. (1) Yes or (2) No")
            .Send("yes")
                .AssertReply("confirmation: True")
                .AssertReply("yes or no (1) Yes or (2) No")
            .Send("nope")
                .AssertReply("confirmation: False")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_ChoiceInput()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                AutoEndDialog = false,
                Steps = new List<IDialog>()
                {
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Not a color. Please select a color:"),
                        Choices = new List<Choice>() { new Choice("red"), new Choice("green"), new Choice("blue") },
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Please select a color:"),
                        Choices = new List<Choice>() { new Choice("red"), new Choice("green"), new Choice("blue") },
                        AlwaysPrompt = true,
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Please select a color:"),
                        Choices = new List<Choice>() { new Choice("red"), new Choice("green"), new Choice("blue") },
                        AlwaysPrompt = true,
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                }
            };

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Please select a color: (1) red, (2) green, or (3) blue")
            .Send("asdasd")
                .AssertReply("Not a color. Please select a color: (1) red, (2) green, or (3) blue")
            .Send("blue")
                .AssertReply("blue")
                .AssertReply("Please select a color: (1) red, (2) green, or (3) blue")
            .Send("red")
                .AssertReply("red")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_ChoiceInput_WithLocale()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                AutoEndDialog = false,
                Steps = new List<IDialog>()
                {
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Not a color. Please select a color:"),
                        Choices = new List<Choice>() { new Choice("red"), new Choice("green"), new Choice("blue") },
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Please select a color:"),
                        Choices = new List<Choice>() { new Choice("red"), new Choice("green"), new Choice("blue") },
                        AlwaysPrompt = true,
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Please select a color:"),
                        Choices = new List<Choice>() { new Choice("red"), new Choice("green"), new Choice("blue") },
                        AlwaysPrompt = true,
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                }
            };

            await CreateFlow(testDialog)
            .Send(BuildMessageActivityWithLocale("hi", "en-US"))
                .AssertReply("Please select a color: (1) red, (2) green, or (3) blue")
            .Send(BuildMessageActivityWithLocale("asdasd", "EN-US"))
                .AssertReply("Not a color. Please select a color: (1) red, (2) green, or (3) blue")
            .Send(BuildMessageActivityWithLocale("blue", "en-us"))
                .AssertReply("blue")
                .AssertReply("Please select a color: (1) red, (2) green, or (3) blue")
            .Send("red")
                .AssertReply("red")
            .StartTestAsync();
        }

        private static IActivity BuildMessageActivityWithLocale(string text, string locale)
        {
            return new Activity()
            {
                Type = ActivityTypes.Message,
                Text = text,
                Locale = locale
            };
        }

        [TestMethod]
        public async Task Step_ChoiceStringInMemory()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                AutoEndDialog = false,
                Steps = new List<IDialog>()
                {
                    new SetProperty()
                    {
                        Value = "json('[\"red\", \"green\", \"blue\"]')",
                        Property = "user.choices"
                    },
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Not a color. Please select a color:"),
                        ChoicesProperty = "user.choices",
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Please select a color:"),
                        ChoicesProperty = "user.choices",
                        AlwaysPrompt = true,
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Please select a color:"),
                        ChoicesProperty = "user.choices",
                        AlwaysPrompt = true,
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                }
            };

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Please select a color: (1) red, (2) green, or (3) blue")
            .Send("asdasd")
                .AssertReply("Not a color. Please select a color: (1) red, (2) green, or (3) blue")
            .Send("3")
                .AssertReply("blue")
                .AssertReply("Please select a color: (1) red, (2) green, or (3) blue")
            .Send("red")
                .AssertReply("red")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_ChoicesInMemory()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                AutoEndDialog = false,
                Steps = new List<IDialog>()
                {
                    new SetProperty()
                    {
                        Value = "json('[{\"value\": \"red\"}, {\"value\": \"green\"}, {\"value\": \"blue\"}]')",
                        Property = "user.choices"
                    },
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Not a color. Please select a color:"),
                        ChoicesProperty = "user.choices",
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Please select a color:"),
                        ChoicesProperty = "user.choices",
                        AlwaysPrompt = true,
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                    new ChoiceInput()
                    {
                        Property = "user.color",
                        Prompt = new ActivityTemplate("Please select a color:"),
                        UnrecognizedPrompt = new ActivityTemplate("Please select a color:"),
                        ChoicesProperty = "user.choices",
                        AlwaysPrompt = true,
                        Style = ListStyle.Inline
                    },
                    new SendActivity("{user.color}"),
                }
            };

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Please select a color: (1) red, (2) green, or (3) blue")
            .Send("asdasd")
                .AssertReply("Not a color. Please select a color: (1) red, (2) green, or (3) blue")
            .Send("3")
                .AssertReply("blue")
                .AssertReply("Please select a color: (1) red, (2) green, or (3) blue")
            .Send("red")
                .AssertReply("red")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_NumberInput()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                AutoEndDialog = false
            };

            testDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new NumberInput()
                        {
                            Prompt = new ActivityTemplate("Please enter your age."),
                            UnrecognizedPrompt = new ActivityTemplate("The value entered must be greater than 0 and less than 150."),
                            Property = "user.userProfile.Age",
                            OutputFormat = NumberOutputFormat.Integer,
                            Validations = new List<string>()
                            {
                                "turn.value > 0 && turn.value < 150"
                            }
                        },
                        new SendActivity("I have your age as {user.userProfile.Age}."),
                    })
            });

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Please enter your age.")
            .Send("1000")
                .AssertReply("The value entered must be greater than 0 and less than 150.")
            .Send("15.3")
                .AssertReply("I have your age as 15.")
            .Send("hi")
                .AssertReply("I have your age as 15.")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_DatetimeInput()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                AutoEndDialog = false
            };

            testDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new DateTimeInput()
                        {
                            Prompt = new ActivityTemplate("Please enter a date."),
                            Value = "user.date",
                            Property = "user.date",
                        },
                        new SendActivity("You entered {user.date[0].Value}"),
                    })
            });

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Please enter a date.")
            .Send("June 1st 2019")
                .AssertReply("You entered 2019-06-01")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_TextInputWithInvalidPrompt()
        {
            var testDialog = new AdaptiveDialog("planningTest");

            testDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new IfCondition()
                        {
                            Condition = "user.name == null",
                            Steps = new List<IDialog>()
                            {
                                new TextInput()
                                {
                                    Prompt = new ActivityTemplate("Hello, what is your name?"),
                                    UnrecognizedPrompt = new ActivityTemplate("How should I call you?"),
                                    InvalidPrompt = new ActivityTemplate("That does not soud like a name"),
                                    Property = "user.name",
                                    Validations = new List<string>()
                                    {
                                        "turn.value.Length > 3"
                                    }
                                }
                            }
                        },
                        new SendActivity("Hello {user.name}, nice to meet you!")
                    })});

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Hello, what is your name?")
            .Send("c")
                .AssertReply("That does not soud like a name")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_EditStepReplaceSequence()
        {
            var testDialog = new AdaptiveDialog("planningTest")
            {
                Recognizer = new RegexRecognizer()
                {
                    Intents = new Dictionary<string, string>() {
                        { "Replace", "(?i)replace" }
                    }
                }
            };

            testDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule()
                {
                    Steps = new List<IDialog>()
                    {
                        new TextInput() {
                            Prompt = new ActivityTemplate("Say replace to replace these steps"),
                            Property = "turn.tempInput"
                        },
                        new SendActivity("You should not see this step if you said replace"),
                        new RepeatDialog()
                    }
                },
                new IntentRule() {
                    Intent = "Replace",
                    Steps = new List<IDialog>() {
                        new SendActivity("I'm going to replace the original steps via EditSteps"),
                        new EditSteps() {
                            ChangeType = StepChangeTypes.ReplaceSequence,
                            Steps = new List<IDialog>() {
                                new SendActivity("New steps..."),
                                new TextInput() {
                                    Prompt = new ActivityTemplate("What's your name?"),
                                    Property = "turn.tempInput"
                                }
                            }
                        }
                    }
                }
            });

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Say replace to replace these steps")
            .Send("replace")
                .AssertReply("I'm going to replace the original steps via EditSteps")
                .AssertReply("New steps...")
                .AssertReply("What's your name?")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_DoSteps()
        {
            var testDialog = new AdaptiveDialog("planningTest");

            testDialog.Recognizer = new RegexRecognizer() { Intents = new Dictionary<string, string>() { { "JokeIntent", "joke" } } };

            testDialog.AddRules(new List<IRule>()
            {
                new IntentRule("JokeIntent",
                    steps: new List<IDialog>()
                    {
                        new SendActivity("Why did the chicken cross the road?"),
                        new EndTurn(),
                        new SendActivity("To get to the other side")
                    }),
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new IfCondition()
                        {
                            Condition = "user.name == null",
                            Steps = new List<IDialog>()
                            {
                                new TextInput()
                                {
                                    Prompt  = new ActivityTemplate("Hello, what is your name?"),
                                    OutputBinding = "user.name"
                                }
                            }
                        },
                        new SendActivity("Hello {user.name}, nice to meet you!")
                    })});

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Hello, what is your name?")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
            .Send("Do you know a joke?")
                .AssertReply("Why did the chicken cross the road?")
            .Send("Why?")
                .AssertReply("To get to the other side")
            .Send("hi")
                .AssertReply("Hello Carlos, nice to meet you!")
            .StartTestAsync();
        }


        [TestMethod]
        public async Task Step_BeginDialog()
        {
            var tellJokeDialog = new AdaptiveDialog("TellJokeDialog");
            tellJokeDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new SendActivity("Why did the chicken cross the road?"),
                        new EndTurn(),
                        new SendActivity("To get to the other side")
                    }
                 )
            });

            var askNameDialog = new AdaptiveDialog("AskNameDialog")
            {
                Steps = new List<IDialog>()
                {
                    new IfCondition()
                    {
                        Condition = "user.name == null",
                        Steps = new List<IDialog>()
                        {
                            new TextInput()
                            {
                                Prompt  = new ActivityTemplate("Hello, what is your name?"),
                                OutputBinding = "user.name"
                            }
                        }
                    },
                    new SendActivity("Hello {user.name}, nice to meet you!")
                }
            };


            var testDialog = new AdaptiveDialog("planningTest");
            testDialog.AutoEndDialog = false;

            testDialog.Recognizer = new RegexRecognizer() { Intents = new Dictionary<string, string>() { { "JokeIntent", "joke" } } };

            testDialog.Steps = new List<IDialog>()
                    {
                        new SendActivity("I'm a joke bot. To get started say 'tell me a joke'"),
                        new BeginDialog() { Dialog = askNameDialog }
                    };

            testDialog.AddRules(new List<IRule>()
            {
                new IntentRule("JokeIntent",
                    steps: new List<IDialog>()
                    {
                        new BeginDialog() { Dialog = tellJokeDialog }
                    }),
                new UnknownIntentRule(
                    steps: new List<IDialog>()
                    {
                        new SendActivity("I'm a joke bot. To get started say 'tell me a joke'")
                    }),
            });

            await CreateFlow(testDialog)
            .SendConversationUpdate()
                .AssertReply("I'm a joke bot. To get started say 'tell me a joke'")
                .AssertReply("Hello, what is your name?")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
            .Send("Cool")
                .AssertReply("I'm a joke bot. To get started say 'tell me a joke'")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_ReplaceDialog()
        {
            var tellJokeDialog = new AdaptiveDialog("TellJokeDialog");
            tellJokeDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new SendActivity("Why did the chicken cross the road?"),
                        new EndTurn(),
                        new SendActivity("To get to the other side")
                    }
                 )
            });

            var askNameDialog = new AdaptiveDialog("AskNameDialog")
            {
                Steps = new List<IDialog>()
                {
                    new IfCondition()
                    {
                        Condition = "user.name == null",
                        Steps = new List<IDialog>()
                        {
                            new TextInput()
                            {
                                Prompt = new ActivityTemplate("Hello, what is your name?"),
                                UnrecognizedPrompt = new ActivityTemplate("How should I call you?"),
                                InvalidPrompt  = new ActivityTemplate("That does not soud like a name"),
                                Property = "user.name",
                            }
                        }
                    },
                    new SendActivity("Hello {user.name}, nice to meet you!")
                }
            };

            var testDialog = new AdaptiveDialog("planningTest");
            testDialog.AutoEndDialog = false;
            testDialog.Recognizer = new RegexRecognizer() { Intents = new Dictionary<string, string>() { { "JokeIntent", "joke" } } };
            testDialog.Steps = new List<IDialog>()
            {
                new SendActivity("I'm a joke bot. To get started say 'tell me a joke'"),
                new ReplaceDialog("AskNameDialog")
            };

            testDialog.AddRules(new List<IRule>()
            {
                new IntentRule("JokeIntent",
                    steps: new List<IDialog>()
                    {
                        new ReplaceDialog("TellJokeDialog")
                    }),
            });

            testDialog.AddDialog(new List<IDialog>()
            {
                tellJokeDialog,
                askNameDialog
            });

            await CreateFlow(testDialog)
            .SendConversationUpdate()
                .AssertReply("I'm a joke bot. To get started say 'tell me a joke'")
                .AssertReply("Hello, what is your name?")
            .Send("Carlos")
                .AssertReply("Hello Carlos, nice to meet you!")
            .Send("Do you know a joke?")
                .AssertReply("Why did the chicken cross the road?")
            .Send("Why?")
                .AssertReply("To get to the other side")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_EndDialog()
        {
            var testDialog = new AdaptiveDialog("planningTest");

            testDialog.Recognizer = new RegexRecognizer() { Intents = new Dictionary<string, string>() { { "EndIntent", "end" } } };

            var tellJokeDialog = new AdaptiveDialog("TellJokeDialog");
            tellJokeDialog.AddRules(new List<IRule>()
            {
                new IntentRule("EndIntent",
                    steps: new List<IDialog>()
                    {
                        new EndDialog()
                    }),
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new SendActivity("Why did the chicken cross the road?"),
                        new EndTurn(),
                        new SendActivity("To get to the other side")
                    })
            });
            tellJokeDialog.Recognizer = new RegexRecognizer() { Intents = new Dictionary<string, string>() { { "EndIntent", "end" } } };

            testDialog.AddRules(new List<IRule>()
            {
                new UnknownIntentRule(
                    new List<IDialog>()
                    {
                        new BeginDialog() { Dialog = tellJokeDialog },
                        new SendActivity("You went out from ask name dialog.")
                    })
            });

            await CreateFlow(testDialog)
            .Send("hi")
                .AssertReply("Why did the chicken cross the road?")
            .Send("end")
                .AssertReply("You went out from ask name dialog.")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_RepeatDialog()
        {
            var testDialog = new AdaptiveDialog("testDialog")
            {
                Steps = new List<IDialog>()
                {
                    new TextInput() { Prompt = new ActivityTemplate("Hello, what is your name?"), OutputBinding = "user.name", Value = "user.name" },
                    new SendActivity("Hello {user.name}, nice to meet you!"),
                    new EndTurn(),
                    new RepeatDialog()
                }
            };

            await CreateFlow(testDialog)
                .Send("hi")
                    .AssertReply("Hello, what is your name?")
                .Send("Carlos")
                    .AssertReply("Hello Carlos, nice to meet you!")
                .Send("hi")
                    .AssertReply("Hello Carlos, nice to meet you!")
                .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_EmitEvent()
        {
            var convoState = new ConversationState(new MemoryStorage());
            var userState = new UserState(new MemoryStorage());

            var rootDialog = new AdaptiveDialog("root")
            {
                Steps = new List<IDialog>()
                {
                    new BeginDialog()
                    {
                        Dialog = new AdaptiveDialog("outer")
                        {
                            AutoEndDialog = false,
                            Recognizer = new RegexRecognizer()
                            {
                                Intents = new Dictionary<string, string>()
                                {
                                    { "EmitIntent", "emit" },
                                    { "CowboyIntent", "moo" }
                                }
                            },
                            Rules = new List<IRule>()
                            {
                                new IntentRule(intent: "CowboyIntent")
                                {
                                    Steps = new List<IDialog>()
                                    {
                                        new SendActivity("Yippee ki-yay!")
                                    }
                                },
                                new IntentRule(intent: "EmitIntent")
                                {
                                    Steps = new List<IDialog>()
                                    {
                                        new EmitEvent()
                                        {
                                            EventName = "CustomEvent",
                                            BubbleEvent = true,
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                Rules = new List<IRule>()
                {
                    new EventRule()
                    {
                        Events = new List<string>() { "CustomEvent" },
                        Steps = new List<IDialog>()
                        {
                            new SendActivity("CustomEventFired")
                        }
                    }
                }
            };

            await CreateFlow(rootDialog)
            .Send("moo")
                .AssertReply("Yippee ki-yay!")
            .Send("emit")
                .AssertReply("CustomEventFired")
            .Send("moo")
                .AssertReply("Yippee ki-yay!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_Foreach()
        {
            var convoState = new ConversationState(new MemoryStorage());
            var userState = new UserState(new MemoryStorage());

            var rootDialog = new AdaptiveDialog("root")
            {
                Steps = new List<IDialog>()
                {
                    new InitProperty()
                    {
                        Property = "dialog.todo",
                        Type = "Array"
                    },

                    new EditArray()
                    {
                        ArrayProperty = "dialog.todo",
                        ChangeType = EditArray.ArrayChangeType.Push,
                        Value = "1"
                    },

                    new EditArray()
                    {
                        ArrayProperty = "dialog.todo",
                        ChangeType = EditArray.ArrayChangeType.Push,
                        Value = "2"
                    },

                    new EditArray()
                    {
                        ArrayProperty = "dialog.todo",
                        ChangeType = EditArray.ArrayChangeType.Push,
                        Value = "3"
                    },

                    new Foreach()
                    {
                        ListProperty = "dialog.todo",
                        Steps = new List<IDialog>()
                        {
                            new SendActivity("index is: {dialog.index} and value is: {dialog.value}")
                        }
                    }
                }
            };

            await CreateFlow(rootDialog)
            .Send("hi")
                .AssertReply("index is: 0 and value is: 1")
                .AssertReply("index is: 1 and value is: 2")
                .AssertReply("index is: 2 and value is: 3")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task Step_ForeachPage()
        {
            var convoState = new ConversationState(new MemoryStorage());
            var userState = new UserState(new MemoryStorage());

            var rootDialog = new AdaptiveDialog("root")
            {
                Steps = new List<IDialog>()
                {
                    new InitProperty()
                    {
                        Property = "dialog.todo",
                        Type = "Array"
                    },

                    new EditArray()
                    {
                        ArrayProperty = "dialog.todo",
                        ChangeType = EditArray.ArrayChangeType.Push,
                        Value = "1"
                    },

                    new EditArray()
                    {
                        ArrayProperty = "dialog.todo",
                        ChangeType = EditArray.ArrayChangeType.Push,
                        Value = "2"
                    },

                    new EditArray()
                    {
                        ArrayProperty = "dialog.todo",
                        ChangeType = EditArray.ArrayChangeType.Push,
                        Value = "3"
                    },

                    new EditArray()
                    {
                        ArrayProperty = "dialog.todo",
                        ChangeType = EditArray.ArrayChangeType.Push,
                        Value = "4"
                    },

                    new EditArray()
                    {
                        ArrayProperty = "dialog.todo",
                        ChangeType = EditArray.ArrayChangeType.Push,
                        Value = "5"
                    },

                    new EditArray()
                    {
                        ArrayProperty = "dialog.todo",
                        ChangeType = EditArray.ArrayChangeType.Push,
                        Value = "6"
                    },

                    new ForeachPage()
                    {
                        ListProperty = "dialog.todo",
                        PageSize = 3,
                        ValueProperty = "dialog.page",
                        Steps = new List<IDialog>()
                        {
                            new SendActivity("This page have 3 items"),
                            new Foreach()
                            {
                                ListProperty = "dialog.page",
                                Steps = new List<IDialog>()
                                {
                                    new SendActivity("index is: {dialog.index} and value is: {dialog.value}")
                                }
                            }
                        }
                    }
                }
            };

            await CreateFlow(rootDialog)
            .Send("hi")
                .AssertReply("This page have 3 items")
                .AssertReply("index is: 0 and value is: 1")
                .AssertReply("index is: 1 and value is: 2")
                .AssertReply("index is: 2 and value is: 3")
                .AssertReply("This page have 3 items")
                .AssertReply("index is: 0 and value is: 4")
                .AssertReply("index is: 1 and value is: 5")
                .AssertReply("index is: 2 and value is: 6")
            .StartTestAsync();
        }
    }
}
