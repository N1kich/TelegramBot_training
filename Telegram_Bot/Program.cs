using System;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.InputFiles;

namespace Telegram_Bot
{
    class Program
    {
        public enum FileType
        {
            Document,
            Audio,
            Video,
            Photo,
        }
        /// <summary>
        /// Main Task of our bot
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async Task Main(string[] args)
        {
            //bot token
            string botToken = "your bot token";

            //absolute path || relative path
            

            const string relativePath = @"DownloadFiles\";
            string path = Path.GetFullPath(relativePath);

            //permission to download files from user, when you press /menu -> true
            bool IsMenuButtonSelected = false;
            
            // initialize and set base settings for bot from manual
            var telegramBot = new TelegramBotClient(botToken);
            using var cts = new CancellationTokenSource();

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { } // receive all update types
            };

            //set up dictionaries once. emojis and markUpKeyboard
            Dictionary<string, string> emojis = SetEmojis();
            Dictionary<string, ReplyKeyboardMarkup> keyboards = SetBotKeyboardButtons();

            telegramBot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token);

            //get response from bot
            var me = await telegramBot.GetMeAsync();

            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

            // Send cancellation request to stop bot
            cts.Cancel();
            

            async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
            {
                
                //if update type is message, then work with message data
                if (update.Type == UpdateType.Message)
                {
                    // if message is a text, handle it
                    if (update.Message.Type == MessageType.Text)
                    {
                        //just cosmetic output to console
                        var chatId = update.Message.Chat.Id;
                        var messageText = update.Message.Text;
                        Console.WriteLine($"Received a '{messageText}' message in chat {chatId}. By {update.Message.From.Username}");
                        
                        //define if text message is a command
                        await TextOptionsAsync(botClient, update, cancellationToken);
                    }
                    else
                    {
                        //if message type != text, try to donwload it
                        await FileDownloaderHandlerAsync(botClient, update, cancellationToken, path, emojis, IsMenuButtonSelected);
                    }
                    
                }
                //if update type is CallbackQuery 
                if (update.Type == UpdateType.CallbackQuery)
                {
                    //define CallBackData 
                    string fileToUpload = update.CallbackQuery.Data;
                    Console.WriteLine(fileToUpload);

                    //upload files based on CallBack Data
                    await UploadChoosenFileAsync(fileToUpload, path, (TelegramBotClient)botClient, update);
                }
            }

            Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
            {
                var ErrorMessage = exception switch
                {
                    ApiRequestException apiRequestException
                        => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                    _ => exception.ToString()
                };

                Console.WriteLine(ErrorMessage);
                return Task.CompletedTask;
            }

            //Task to handle text command
            async Task TextOptionsAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
            {
                var messageText = update.Message.Text;
                // handle text form message
                switch (messageText)
                {
                    //case to send greetigs to user
                    case "/start":
                        {
                            await DescriprionStartAsync(botClient, update, cancellationToken, keyboards["startButtons"], emojis);
                            IsMenuButtonSelected = false;
                            break;
                        }
                    //case to send descriptions of functions which bot can process
                    case "/menu":
                        {
                            await MenuDescriptionAsync(botClient, update, cancellationToken, keyboards["menuButtons"], emojis, path);
                            IsMenuButtonSelected = true;
                            break;
                        }
                    //case to send info about
                    case "/info":
                        {
                            await botClient.SendTextMessageAsync(update.Message.Chat.Id, "test file-trading bot", cancellationToken: cancellationToken);
                            IsMenuButtonSelected = false;
                            break;
                        }
                    //case to send list of files, this command could be invoke in /menu from keyboard button
                    case "I want to know, which files are already in the bot's storage":
                        {
                            await SendListOfFilesAsync(path, (TelegramBotClient)botClient, update, cancellationToken, emojis);
                            break;
                        }
                    default:
                        Message defaultMessage = await botClient.SendTextMessageAsync(update.Message.Chat.Id, "I dont understand your commands" + emojis["hmmEmoji"] + ".\n Use keyboard buttons to navigate!" +
                            emojis["coolMan"], cancellationToken: cancellationToken);
                        defaultMessage = await botClient.SendStickerAsync(update.Message.Chat.Id, sticker: "CAACAgIAAxkBAAEDy3Vh-w1UvK764n4JWmM-v2e9rndxLQAC6BUAAiMlyUtQqGgG1fAXAAEjBA", cancellationToken: cancellationToken);
                        break;
                }               
            }

            
        }

        /// <summary>
        /// method to initialize keyboards
        /// </summary>
        /// <returns></returns>
        static  Dictionary<string, ReplyKeyboardMarkup> SetBotKeyboardButtons()
        {
            Dictionary<string, ReplyKeyboardMarkup> keyboards = new()
            {
                {"startButtons", new ReplyKeyboardMarkup(new[] { new KeyboardButton[] {"/start" }, new KeyboardButton[] {"/menu" }, new KeyboardButton[] {"/info" } }){ResizeKeyboard = true } },
                {"menuButtons", new ReplyKeyboardMarkup(new[] { new KeyboardButton[] {"I want to know, which files are already in the bot's storage" } }){ResizeKeyboard = true } }, 
            };

            
            return keyboards;

        }

        
        /// <summary>
        /// Define fileExtension
        /// </summary>
        /// <param name="Path"></param>
        /// <returns></returns>
        static Dictionary<string, FileType> GetFileExtension(string Path)
        {
            // get all files in special folder and define extension
            string[] fileNames = Directory.GetFiles(Path);

            Dictionary<string, FileType> files = new Dictionary<string, FileType>();

            if (fileNames != default(string[]))
            {
                for (int i = 0; i < fileNames.Length; i++)
                {
                    string fileExtension = fileNames[i].Substring(fileNames[i].LastIndexOf('.'));
                    files.Add(fileNames[i].Substring(fileNames[i].LastIndexOf(@"\") + 1),GetFileType(fileExtension));
                }
                
            }
            return files;
        }


        /// <summary>
        /// return enum type of file extension
        /// </summary>
        /// <param name="Extension"></param>
        /// <returns></returns>
        static FileType GetFileType(string Extension)
        {
            switch (Extension)
            {
                case ".jpg":
                    return FileType.Photo;
                case ".mp3":
                    return FileType.Audio;
                case ".mp4":
                    return FileType.Video;
                default:
                    return FileType.Document;
            }
        }


        /// <summary>
        /// method to set dictionary of emojis, runs once 
        /// </summary>
        /// <returns></returns>
        static Dictionary<string, string> SetEmojis()
        {
            Dictionary<string, string> emojis = new Dictionary<string, string>()
            {
                {"pen", char.ConvertFromUtf32(0x270F) },
                {"checkMark",char.ConvertFromUtf32(0x2705)  },
                {"bicycleMan",char.ConvertFromUtf32(0x267F)  },
                {"hmmEmoji", char.ConvertFromUtf32(0x1F914)  },
                {"coolMan",  char.ConvertFromUtf32(0x1F60E) },
                {"voiceMessage", char.ConvertFromUtf32(0x1F3A4) },
                {"video", char.ConvertFromUtf32(0x1F3A6)},
                {"audio", char.ConvertFromUtf32(0x1F3B5) },
                {"document", char.ConvertFromUtf32(0x1F4C3) },
                {"photo", char.ConvertFromUtf32(0x1F4F7) }

            };

            return emojis;
        }


        /// <summary>
        /// sends greetings to new user, runs when bot get /start command
        /// </summary>
        /// <param name="botClient"></param>
        /// <param name="update"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="keyboard"></param>
        /// <param name="emojis"></param>
        /// <returns></returns>
        static async Task DescriprionStartAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, ReplyKeyboardMarkup keyboard, Dictionary<string,string> emojis)
        {
            string startDesription = "\tWelcome to the training bot!" + emojis["checkMark"] + "\nDescription of Bot Commands below" + emojis["pen"] +
                "\n/start - say hi to bot and view this message" + emojis["pen"] +
                "\n/menu - show command keyboard" + emojis["pen"] + "\n/info - show info about this bot" + emojis["pen"];

            Message DescriptionMessage = await botClient.SendTextMessageAsync(update.Message.Chat.Id, startDesription, replyMarkup: keyboard, cancellationToken: cancellationToken);
        }


        /// <summary>
        /// description abot functions which bot can process, runs when user send /menu
        /// </summary>
        /// <param name="botClient"></param>
        /// <param name="update"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="keyboard"></param>
        /// <param name="emojis"></param>
        /// <param name="Path"></param>
        /// <returns></returns>
        static async Task MenuDescriptionAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, ReplyKeyboardMarkup keyboard,  Dictionary<string,string> emojis, string Path)
        {
            string descriptionStr = $"\tOn this page you can choose following functions{emojis["bicycleMan"]}\n" +
                $"You can store the following file types:\n{emojis["photo"]} Photos!\n" +
                    $"{emojis["document"]} Documents!\n" +
                    $"{emojis["audio"]} Audio\n " +
                    $"{emojis["video"]} Video and VideoNotes\n" +
                    $"{emojis["voiceMessage"]} VoiceMessage\n"+
                    $"{emojis["bicycleMan"]} Get the names of files from bot's storage{emojis["bicycleMan"]}\n" +
                $"{emojis["bicycleMan"]}Download the file from storage. To begin this operations check the existing files {emojis["bicycleMan"]}";

            Message MenuDescr = await botClient.SendTextMessageAsync(update.Message.Chat.Id, descriptionStr, replyMarkup: keyboard, cancellationToken: cancellationToken);
            
        }


        /// <summary>
        /// async Task to download specific files, define type of message, then define type of files get information about it then download file
        /// bool value uses in order to load files only after the /menu message
        /// </summary>
        /// <param name="botClient"></param>
        /// <param name="update"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="Path"></param>
        /// <param name="emojis"></param>
        /// <param name="IsMenuButtonSelected"></param>
        /// <returns></returns>
        static async Task FileDownloaderHandlerAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string Path, Dictionary<string, string> emojis, bool IsMenuButtonSelected)
        {
            //all supported message types
            if (update.Message.Type == MessageType.Photo || update.Message.Type == MessageType.Video || update.Message.Type == MessageType.VideoNote || update.Message.Type == MessageType.Voice || update.Message.Type == MessageType.Document || update.Message.Type == MessageType.Audio)
            {
                // if user don't click /menu button yet cancel download
                if (!IsMenuButtonSelected)
                {
                    Message sendWarnig = await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"Please choose the /menu to send and store your files in bot's files storage {emojis["coolMan"]}");
                    return;
                }
                switch (update.Message.Type)
                {
                    case MessageType.Photo:
                        {
                            string fileID = update.Message.Photo[^1].FileId;
                            //in case when we cant get the file name from telegram, we use 3 uniqe numbers from file ID
                            string UniqeID = fileID.Substring(fileID.Length/2, 3);
                            await DownloadAsync(fileID, Path + update.Message.Chat.Username + UniqeID  + ".jpg", (TelegramBotClient)botClient);
                            
                            break;
                        }
                    case MessageType.Document:
                        {
                            var fileID = update.Message.Document.FileId;
                            await DownloadAsync(fileID, Path + update.Message.Document.FileName, (TelegramBotClient)botClient);
                            break;
                        }
                    case MessageType.Video:
                        {
                            var fileID = update.Message.Video.FileId;
                            var fileName = update.Message.Video.FileName;
                            await DownloadAsync(fileID, Path + fileName, (TelegramBotClient)botClient);
                            break;
                        }
                    case MessageType.Audio:
                        {
                            var fileID = update.Message.Audio.FileId;
                            var fileName = update.Message.Audio.FileName;
                            await DownloadAsync(fileID, Path + fileName, (TelegramBotClient)botClient);
                            break;
                        }
                    case MessageType.Voice:
                        {
                            var fileID = update.Message.Voice.FileId;
                            string UniqeID = fileID.Substring(fileID.Length - 5);
                            await DownloadAsync(fileID, Path + update.Message.Chat.Username + UniqeID + ".mp3", (TelegramBotClient)botClient);

                            break;
                        }
                    case MessageType.VideoNote:
                        {
                            var fileID = update.Message.VideoNote.FileId;
                            string UniqeID = fileID.Substring(fileID.Length - 5);
                            await DownloadAsync(fileID, Path + update.Message.Chat.Username + UniqeID +  ".mp4", (TelegramBotClient)botClient);
                            
                            break;
                        }      
                }
                Console.WriteLine($"Download the file from  {update.Message.Chat.Username} {update.Message.Chat.Id} - {update.Message.Type}");
                Message message = await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"I download the {update.Message.Type}" + emojis["pen"], cancellationToken: cancellationToken);
            }
            else
            {
                Console.WriteLine($"Unknown type of files");
                Message ExplanationMessage = await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"Unknown type of files {emojis["hmmEmoji"]}\n You can store the following file types:\n{emojis["photo"]} Photos!\n" +
                    $"{emojis["document"]} Documents!\n" +
                    $"{emojis["audio"]} Audio\n " +
                    $"{emojis["video"]} Video and VideoNotes\n" +
                    $"{emojis["voiceMessage"]} VoiceMessage\n", cancellationToken: cancellationToken);
            }


        }

        /// <summary>
        /// task to download file
        /// </summary>
        /// <param name="FileID"></param>
        /// <param name="Path"></param>
        /// <param name="bot"></param>
        /// <returns></returns>
        static async Task DownloadAsync(string FileID, string Path, TelegramBotClient bot)
        {
            var fileInfo = await bot.GetFileAsync(FileID);

            using FileStream fs = new FileStream(Path, FileMode.Create);
            await bot.DownloadFileAsync(fileInfo.FilePath, fs);
        }


        /// <summary>
        /// task to send list of files, before sending updates dictionary of files
        /// </summary>
        /// <param name="path"></param>
        /// <param name="bot"></param>
        /// <param name="update"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="emojis"></param>
        /// <returns></returns>
        static async Task SendListOfFilesAsync(string path, TelegramBotClient bot, Update update, CancellationToken cancellationToken, Dictionary<string,string> emojis)
        {
            //get files
            Dictionary<string, FileType> filesInPath = GetFileExtension(path);

            if (filesInPath.Count == 0)
            {
                _ = await bot.SendTextMessageAsync(update.Message.Chat.Id, $"I dont have any files in my storage {emojis["coolMan"]}", cancellationToken: cancellationToken);
            }
            else
            {
                // build string with all files
                string listOfFiles = $"List of Files below{emojis["hmmEmoji"]}:\n";
                foreach (var files in filesInPath)
                {

                    listOfFiles += GetTheStrWithEmoji(files.Key, files.Value, emojis);

                }
                InlineKeyboardMarkup inlineKeyboard = SetInlineKeyboards(filesInPath, emojis);
                _ = await bot.SendTextMessageAsync(update.Message.Chat.Id, listOfFiles, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);

            }


        }


        /// <summary>
        /// method to built InlineMarkupKeyboard with fileNames and callback data
        /// </summary>
        /// <param name="listOfFiles"></param>
        /// <param name="emojis"></param>
        /// <returns></returns>
        static InlineKeyboardMarkup SetInlineKeyboards(Dictionary <string, FileType> listOfFiles, Dictionary<string,string> emojis)
        {
            //creates list of inline buttons. Each list contains triplet of buttons. Adding list to list of inlinekeyboards array
            int sizeOfDictionary = listOfFiles.Count;
            InlineKeyboardMarkup inlineMarkup;

            List<InlineKeyboardButton> buttons = new List<InlineKeyboardButton>();
            List<InlineKeyboardButton[]> arrayOfButtons = new List<InlineKeyboardButton[]>();
            
            int k = 1;
            int i = 0;
            foreach (var item in listOfFiles)
            {
               // algorithm to collect files in triplets, if amount_of_files % 3 != 0 add 1 or 2 files in last inline string
               //create string with specific emoji and name of file based on file's extension
                string StringWithEmoji = GetTheStrWithEmoji(item.Key, item.Value, emojis);
                if (sizeOfDictionary - 1 == i && k != 3)
                {
                    switch (k)
                    {
                        case 1:
                            {
                                arrayOfButtons.Add(new[] { new InlineKeyboardButton(StringWithEmoji){ CallbackData = item.Key } }) ;
                                break;
                            }
                        case 2:
                            {
                                arrayOfButtons.Add(new[] { buttons[0], new InlineKeyboardButton(StringWithEmoji) { CallbackData = item.Key } });
                                break;
                            }
                        default:
                            break;
                    }
                }
                else
                {
                    if (k <= 3)
                    {
                        buttons.Add(new InlineKeyboardButton(StringWithEmoji)
                        {
                            CallbackData = item.Key
                        });
                        if (k == 3)
                        {
                            arrayOfButtons.Add(new[] { buttons[0], buttons[1], buttons[2] });
                            k = 0;
                            buttons.Clear();
                        }
                    }
                    
                    k++;
                    i++;
                }
                
            }
            //return inline keyboard object
            inlineMarkup = new InlineKeyboardMarkup(arrayOfButtons.ToArray());
            return inlineMarkup;
        }


        /// <summary>
        /// creates string with file type emoji and file name
        /// </summary>
        /// <param name="KeyFromDictionary"></param>
        /// <param name="ValueFromDictionary"></param>
        /// <param name="emojis"></param>
        /// <returns></returns>
        static string GetTheStrWithEmoji(string KeyFromDictionary, FileType ValueFromDictionary, Dictionary<string, string> emojis)
        {
            string strWithEmoji = "";
            
                switch (ValueFromDictionary)
                {
                    case FileType.Document:
                        {
                            strWithEmoji += emojis["document"] + KeyFromDictionary + "\n";
                            break;
                        }
                    case FileType.Audio:
                        {
                            strWithEmoji += emojis["audio"] + KeyFromDictionary + "\n";
                            break;
                        }
                    case FileType.Photo:
                        {
                            strWithEmoji += emojis["photo"] + KeyFromDictionary + "\n";
                            break;
                        }
                    case FileType.Video:
                        {
                            strWithEmoji += emojis["video"] + KeyFromDictionary + "\n";
                            break;
                        }
                }

            return strWithEmoji;
        }

        /// <summary>
        /// Task to upload choosen file 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="Path"></param>
        /// <param name="bot"></param>
        /// <param name="update"></param>
        /// <returns></returns>
        static async Task UploadChoosenFileAsync(string fileName, string Path, TelegramBotClient bot, Update update)
        {
            string fileExtension = fileName.Substring(fileName.LastIndexOf('.'));
            FileType typeOfCallbackFile = GetFileType(fileExtension);
            using (FileStream stream = System.IO.File.OpenRead(Path + fileName))
            {
                InputOnlineFile inputOnlineFile = new InputOnlineFile(stream, fileName);
                switch (typeOfCallbackFile)
                {
                    case FileType.Document:
                        await bot.SendDocumentAsync(update.CallbackQuery.Message.Chat.Id, inputOnlineFile);
                        break;
                    case FileType.Audio:
                        await bot.SendAudioAsync(update.CallbackQuery.Message.Chat.Id, inputOnlineFile);
                        break;
                    case FileType.Video:
                        await bot.SendVideoAsync(update.CallbackQuery.Message.Chat.Id, inputOnlineFile,supportsStreaming:true);
                        break;
                    case FileType.Photo:
                        await bot.SendPhotoAsync(update.CallbackQuery.Message.Chat.Id, inputOnlineFile);
                        break;
                    default:
                        break;
                }
            }
           
        }

    }
}
