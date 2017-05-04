/*
A .NET Core TOTP (2FA) Manager with encrypted file storage
Version: 1.0

Encryption: AES (MODE_CBC) with HMAC authentication based on https://gist.github.com/jbtule/4336842

This work (A TOTP Manager with encrypted file storage), is free of known copyright restrictions.
http://creativecommons.org/publicdomain/mark/1.0/ 

BASIC USAGE:
Display list of saved totp codes: -pw {password}
Add new totp secret: -pw {password} -a {title} {base32 totp secret}
*/

using System;
using System.Collections.Generic;
using OtpSharp;
using Newtonsoft.Json;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;

namespace ConsoleApplication
{
    public class Program
    {
        private static string FILE_PATH = "/Users/williamroscoe/Will/Code/shareddata/"; //.GetFolderPath(Environment.SpecialFolder.Personal);
        private static string ENCRYPTEDKEYS_FILENAME = FILE_PATH + "totp_encrypted.txt";
        private static int DEFAULT_TOTP_DIGITS = 6;

        public static void Main(string[] args)
        {
            Console.WriteLine(); // spacing
            string _password = string.Empty;
            string _item_name = string.Empty;
            string _item_secret = string.Empty;
            int _item_digits = 0;
            int _selected_id = 0;

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: true);
            var password = app.Option("-pw |--password <password>", "The password to decrypt the data", CommandOptionType.SingleValue);
            var add_atrib_data = app.Argument("-a", "Add new totp item: {title} {base32 totp secret} {digits (optional)}", true);
            var add = app.Option("-a |--add", "Add new totp item: {title} {base32 totp secret} {digits (optional)}", CommandOptionType.NoValue);
            var item_name = app.Option("-title |--title", "Add new totp item title", CommandOptionType.SingleValue);
            var item_secret = app.Option("-secret |--secret", "Add new totp item secret", CommandOptionType.SingleValue);
            var item_digits = app.Option("-digits |--digits", "Add new totp item digits", CommandOptionType.SingleValue);
            var selected_id = app.Option("-id |--ident", "ID of item to process", CommandOptionType.SingleValue);
            var displayorig = app.Option("-d | --displayorig", "Display original data.", CommandOptionType.NoValue);
            var update = app.Option("-u | --update", "Update an item by ID: -id {ID} -title {title} -secret {base32 totp secret} -digits {digits}", CommandOptionType.NoValue);
            var delete = app.Option("-del |--delete", "Delete an item by ID: -id {ID}", CommandOptionType.NoValue);
            var password_update = app.Option("-pu |--passwordupdate", "Update encryption password: {new password}", CommandOptionType.SingleValue);

            app.HelpOption("-? | -hh | --hhelp");
            app.OnExecute(() =>
            {
                if (password.HasValue())
                {
                    _password = password.Value();
                    if (selected_id.HasValue())
                    {
                        if (!Int32.TryParse(selected_id.Value(), out _selected_id))
                        {
                            _selected_id = 0;
                        }
                    }
                    // item data for adding or editing
                    if (item_name.HasValue())
                    {
                        _item_name = item_name.Value();
                    }
                    if (item_secret.HasValue())
                    {
                        _item_secret = item_secret.Value();
                    }
                    if (item_digits.HasValue())
                    {
                        if (!Int32.TryParse(item_digits.Value(), out _item_digits))
                        {
                            _item_digits = 0;
                        }
                    }
                    // main logic
                    if (displayorig.HasValue())
                    {
                        DisplayUnencryptedData(_password);
                    }                  
                    else if (add.HasValue())
                    {
                        if (add_atrib_data.Values.Count > 0 && string.IsNullOrEmpty(_item_name)) // if individual items not specified then get from add_data
                        {
                            _item_name = add_atrib_data.Values[0];
                            if (add_atrib_data.Values.Count > 1)
                            {
                                _item_secret = add_atrib_data.Values[1];
                                if (add_atrib_data.Values.Count > 2)
                                {
                                    if (!Int32.TryParse(add_atrib_data.Values[2], out _item_digits))
                                    {
                                        _item_digits = 0;
                                    }
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(_item_name) && !string.IsNullOrEmpty(_item_secret))
                        {
                            AddNewItem(_password, _item_name, _item_secret, _item_digits);
                            DisplayTOTPList(_password);
                        }
                        else
                        {
                            Console.WriteLine("Missing item data!");
                        }
                    }
                    else if (update.HasValue())
                    {
                        if (_selected_id > 0)
                        {
                            if (!string.IsNullOrEmpty(_item_name) || !string.IsNullOrEmpty(_item_secret) || _item_digits > 0)
                            {
                                UpdateItem(_password, _selected_id, _item_name, _item_secret, _item_digits);
                                DisplayTOTPList(_password);
                            }
                            else
                            {
                                Console.WriteLine("Missing items to edit!");
                            }
                        }
                        else
                        {
                            Console.WriteLine("ID missing!");
                        }
                    }
                    else if (delete.HasValue())
                    {
                        if (_selected_id > 0)
                        {
                            DeleteItem(_password, _selected_id);
                            DisplayTOTPList(_password);
                        }
                        else
                        {
                            Console.WriteLine("ID missing!");
                        }
                    }
                    else if (password_update.HasValue())
                    {
                        UpdatePassword(_password, password_update.Value());
                    }
                    else
                    {
                        DisplayTOTPList(_password);
                    }
                }
                else
                {
                    Console.WriteLine("Password not specified!");
                    Environment.Exit(-1); // Do not continue
                }
                
                return 0;
            });
            app.Execute(args);

            Console.WriteLine(); // spacing
        }

        private static void DisplayTOTPList(string _password)
        {
            var model = LoadAndDecryptToModel(_password, null);
            var _name_column_spacing = model.data.Max(x => x.name.Length) + 4;
            
            Console.WriteLine(string.Format("{0:2}: {1}{2}{3}", "ID", "TITLE", new String(' ', (_name_column_spacing - "TITLE".Length)) , "TOKEN"));
            Console.WriteLine("");

            int item_count = 1;
            foreach(var item in model.data)
            {
                Totp otp = new Totp(ToBytes(item.secret.Replace(" ", "")));
                string totpString = otp.ComputeTotp();
                string remainingSeconds = otp.RemainingSeconds().ToString();
                Console.WriteLine(string.Format("{0,2}: {1}{2}{3}{4}Remaining Secs: {5}", item_count, item.name, new String(' ', (_name_column_spacing - item.name.Length)) , totpString, new String(' ', 6), remainingSeconds));
                item_count++;
            }
        }

        private static void AddNewItem(string _password, string item_name, string item_secret, int item_digits)
        {
            if (!string.IsNullOrEmpty(item_name) && !string.IsNullOrEmpty(item_secret))
            {
                var model = LoadAndDecryptToModel(_password, null);
                
                if (item_digits < 1)
                {
                    item_digits = DEFAULT_TOTP_DIGITS;
                }
                var new_item = new TotpObject()
                {
                    name = item_name,
                    secret = item_secret,
                    digits = item_digits
                };

                model.data.Add(new_item);

                SaveAndEncryptObjectToFile(_password, model, "*** New item saved ***");
            }
            else
            {
                Console.WriteLine("Missing item data!");
            }
        }

        private static void UpdateItem(string _password, int _id, string item_name, string item_secret, int item_digits)
        {
            var model = LoadAndDecryptToModel(_password, null);
            if (_id > 0 && model.data.Count >= _id)
            {
                if (!string.IsNullOrEmpty(item_name))
                {
                    model.data[_id - 1].name = item_name;
                }
                if (!string.IsNullOrEmpty(item_secret))
                {
                    model.data[_id - 1].secret = item_secret;
                }
                if (item_digits > 0)
                {
                    model.data[_id - 1].digits = item_digits;
                }

                SaveAndEncryptObjectToFile(_password, model);
            }
            else
            {
                Console.WriteLine("ID not valid!");
            }
        }

        private static void DeleteItem(string _password, int _id)
        {
            var model = LoadAndDecryptToModel(_password, null);

            if (_id > 0 && model.data.Count >= _id)
            {
                model.data.RemoveAt(_id - 1);
                SaveAndEncryptObjectToFile(_password, model, "*** Item Deleted ***");
            }
            else
            {
                Console.WriteLine("ID not valid!");
            }
        }

        private static void UpdatePassword(string _password, string _new_password)
        {
            var model = LoadAndDecryptToModel(_password, null);
            SaveAndEncryptObjectToFile(_new_password, model, "*** Password updated ***");
        }

        private static void DisplayUnencryptedData(string _password, bool use_file_flag = false)
        {
            Console.Write(LoadAndDecryptToString(_password, null));
        }


        /*---------------------------------
        * LOADING DATA FUNCTIONS - BEGIN 
        * - - - - - - - - - - - - - - - - */

        private static string LoadDataFromFile()
        {
            string result = string.Empty;;
            if (System.IO.File.Exists(ENCRYPTEDKEYS_FILENAME))
            {
                result = System.IO.File.ReadAllText(ENCRYPTEDKEYS_FILENAME);
            }
            return result;
        }

        private static JsonObject LoadAndDecryptToModel(string _password, string _additional_argv1)
        {
            var json_object = new JsonObject();
            var unencrypted_data = LoadAndDecryptToString(_password, _additional_argv1);
            if (!string.IsNullOrEmpty(unencrypted_data))
            {
                json_object  = JsonConvert.DeserializeObject<JsonObject>(unencrypted_data);
            }

            return json_object;
        }

        private static string LoadAndDecryptToString(string _password, string _additional_argv1)
        {
            var encrypted_data = LoadDataFromFile();
            if (!string.IsNullOrEmpty(encrypted_data))
            {
                var result = Encryption.AESThenHMAC.SimpleDecryptWithPassword(encrypted_data, _password);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
                else
                {
                    Console.WriteLine("Key decryption failed!");
                }
            }
            else
            {
                Console.WriteLine("*** Data file empty ***");
                Console.WriteLine();
            }

            return string.Empty;
        }

        /* - - - - - - - - - - - - - - - -
        * LOADING DATA FUNCTIONS - END 
        * -------------------------------*/


        /*---------------------------------
        * SAVING DATA FUNCTIONS - BEGIN 
        * - - - - - - - - - - - - - - - - */

        private static void SaveAndEncryptObjectToFile(string _password, JsonObject model, string success_msg = "")
        {
            // sort items prior to saving
            model.data.Sort((x, y) => string.Compare(x.name, y.name));
            
            // convert object to json
            string json = JsonConvert.SerializeObject(model);
            //Console.Write(json);
            
            // encrypt data
            var encrypted_result = Encryption.AESThenHMAC.SimpleEncryptWithPassword(json, _password);
            // save to file
            System.IO.File.WriteAllText(ENCRYPTEDKEYS_FILENAME, encrypted_result);

            if (!string.IsNullOrEmpty(success_msg))
            {
                Console.WriteLine(success_msg);
                Console.WriteLine(); // spacing
            }
            
        }

        /* - - - - - - - - - - - - - - - -
        * SAVING DATA FUNCTIONS - END 
        * -------------------------------*/
        
        
        private static byte[] ToBytes(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException("input");
            }

            input = input.TrimEnd('='); //remove padding characters
            int byteCount = input.Length * 5 / 8; //this must be TRUNCATED
            byte[] returnArray = new byte[byteCount];

            byte curByte = 0, bitsRemaining = 8;
            int mask = 0, arrayIndex = 0;

            foreach (char c in input)
            {
                int cValue = CharToValue(c);

                if (bitsRemaining > 5)
                {
                    mask = cValue << (bitsRemaining - 5);
                    curByte = (byte)(curByte | mask);
                    bitsRemaining -= 5;
                }
                else
                {
                    mask = cValue >> (5 - bitsRemaining);
                    curByte = (byte)(curByte | mask);
                    returnArray[arrayIndex++] = curByte;
                    curByte = (byte)(cValue << (3 + bitsRemaining));
                    bitsRemaining += 3;
                }
            }

            //if we didn't end with a full byte
            if (arrayIndex != byteCount)
            {
                returnArray[arrayIndex] = curByte;
            }

            return returnArray;
        }

        private static int CharToValue(char c)
        {
            int value = (int)c;

            //65-90 == uppercase letters
            if (value < 91 && value > 64)
            {
                return value - 65;
            }
            //50-55 == numbers 2-7
            if (value < 56 && value > 49)
            {
                return value - 24;
            }
            //97-122 == lowercase letters
            if (value < 123 && value > 96)
            {
                return value - 97;
            }

            throw new ArgumentException("Character is not a Base32 character.", "c");
        }
    }


    public class JsonObject
    {
        public List<TotpObject> data { get; set; }

        public JsonObject()
        {
            data = new List<TotpObject>();
        }
    }

    public class TotpObject
    {
        public string name { get; set; }
        public string secret { get; set; }
        public int digits { get; set; }
    }
    
}
