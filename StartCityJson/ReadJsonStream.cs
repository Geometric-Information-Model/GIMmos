using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StartCityJson
{
    internal static class ReadJsonStream
    {
        public const string ATTRIBUTE_TRANSFORM = "transform";
        public const string ATTRIBUTE_METADATA = "metadata";
        public const string ATTRIBUTE_VERTICES = "vertices";
        public const string ATTRIBUTE_CITY_OBJECTS = "CityObjects";
        public const string ATTRIBUTE_VERSION = "version";
        public static string ReadJsonAttributeFromEndOfFile(FileStream fs, string attributeName, bool fixUpJson = true)
        {
            StringBuilder finalString = new();
            if (fixUpJson) finalString = new("{\"");
            string keyword = attributeName;
            bool foundKeyword = false;
            char finalChar = keyword[^1];
            // Loop 1: Find the keyword "Transform" from the end of the file
            long position = fs.Length - 1;
            byte[] buffer = new byte[1];
            while (position >= 0)
            {
                fs.Seek(position, SeekOrigin.Begin);
                fs.Read(buffer, 0, 1);

                char currentChar = (char)buffer[0];

                if (currentChar == finalChar)
                {
                    string candidateKeyword = ReadPreviousChars(fs, position, keyword.Length - 1) + currentChar;
                    if (candidateKeyword == keyword)
                    {
                        foundKeyword = true;
                        position++;
                        break;
                    }
                }

                position--;
            }

            // Loop 2: Extract information following the keyword until closing bracket
            if (foundKeyword)
            {
                int openBrackets = 0; // Start with 0 as the initial open bracket is not part of the count
                int closeBrackets = 0;

                bool firstOpenBracketFound = false; // To skip the first open bracket after "Transform"

                finalString.Append(keyword);

                while (position < fs.Length && (openBrackets > closeBrackets || !firstOpenBracketFound))
                {
                    fs.Seek(position, SeekOrigin.Begin);
                    fs.Read(buffer, 0, 1);
                    char currentChar = (char)buffer[0];

                    if (currentChar == '{')
                    {
                        openBrackets++;
                        firstOpenBracketFound = true;
                    }
                    else if (currentChar == '}')
                        closeBrackets++;

                    if (openBrackets > closeBrackets || !firstOpenBracketFound)
                        finalString.Append(currentChar);

                    position++;
                }
            }


            if (fixUpJson) finalString.Append("}}");
            return finalString.ToString();
        }

        public static string ReadJsonAttributeFromEndOfFileBuffered(FileStream fs, string attributeName)
        {
            StringBuilder finalString = new StringBuilder("{\"");
            string keyword = attributeName;
            bool foundKeyword = false;
            char finalChar = keyword[^1];
            char firstChar = keyword[0];
            char secondChar = keyword[1];
            // Loop 1: Find the keyword "Transform" from the end of the file
            int bufferSize = 1000;
            long position = Math.Max(0, fs.Length - bufferSize);

            int keywordPosition = -1;

            List<string> stringBuffer = new();
            string previousString = "";
            bool searchedWholeFile = false;

            while (!searchedWholeFile)
            {
                int charactersToRead = (int)Math.Min(bufferSize, fs.Length - position);
                // if the position is zero or negative, we're gonna break cause we scanned the whole file
                if (position <= 0)
                {
                    searchedWholeFile = true;
                    charactersToRead += (int)position; // we subtract however many characters we had extra
                    position = 0;
                }
                fs.Seek(position, SeekOrigin.Begin);


                byte[] buffer = new byte[charactersToRead];
                fs.Read(buffer, 0, charactersToRead);

                string bufferedString = Encoding.UTF8.GetString(buffer);
                stringBuffer.Add(bufferedString);

                string totalString = bufferedString + previousString;

                for (int i = 0; i < Math.Min(bufferedString.Length, totalString.Length  - 1); i++)
                {
                    char currentChar = totalString[i];
                    char secondCurrentChar = totalString[i + 1];
                    if (currentChar == firstChar && secondCurrentChar == secondChar)
                    {
                        string candidateKeyword = totalString[i..(i + keyword.Length)];
                        if (candidateKeyword == keyword)
                        {
                            foundKeyword = true;
                            keywordPosition = i;
                            break;
                        }
                    }
                }

                if (foundKeyword)
                {
                    break;
                }

                previousString = bufferedString;
                position -= bufferSize;
            }

            if (foundKeyword)
            {
                StringBuilder stringBuilder = new StringBuilder();
                for (int i = stringBuffer.Count - 1; i >= 0; i--)
                {
                    stringBuilder.Append(stringBuffer[i]);
                }

                string builtString = stringBuilder.ToString();

                int openBrackets = 0; // Start with 0 as the initial open bracket is not part of the count
                int closeBrackets = 0;

                bool firstOpenBracketFound = false; // To skip the first open bracket after "Transform"

                int startingPosition = keywordPosition + keyword.Length;
                int finalPosition = startingPosition;
                bool foundClosingBrackets = false;
                for (int i = startingPosition; i < builtString.Length; i++)
                {
                    char currentChar = builtString[i];

                    if (currentChar == '{')
                    {
                        openBrackets++;
                        firstOpenBracketFound = true;
                    }
                    else if (currentChar == '}')
                        closeBrackets++;

                    if (openBrackets <= closeBrackets && firstOpenBracketFound)
                    {
                        finalPosition = i + 1;
                        foundClosingBrackets = true;
                        break;
                    }
                }

                if (!foundClosingBrackets)
                {
                    finalPosition = builtString.Length;
                }

                finalString.Append(builtString[keywordPosition..finalPosition]);
                if (foundClosingBrackets) finalString.Append("}");
            }

            // Loop 2: Extract information following the keyword until closing bracket

            return finalString.ToString();
        }

        public static int FindAttributeStart(FileStream fileStream, string attributeName)
        {
            int position = -1;
            string keyword = attributeName;
            fileStream.Seek(0, SeekOrigin.Begin);
            StreamReader reader = new StreamReader(fileStream, Encoding.UTF8);

            char[] bufferword = new char[keyword.Length];
            int charactersRead = 0;

            bool keywordFound = false;
            while(!reader.EndOfStream)
            {
                char currentCharacter = (char)reader.Read();
                if(charactersRead < keyword.Length)
                {
                    bufferword[charactersRead] = currentCharacter;
                    charactersRead++;
                }
                else
                {
                    for(int i =0; i < bufferword.Length-1; i++)
                    {
                        bufferword[i] = bufferword[i+1];
                    }
                    bufferword[^1] = currentCharacter;
                    charactersRead++;
                }

                if (bufferword[0] == keyword[0] && charactersRead > keyword.Length) // potential candidate
                {
                    string candidateWord = new(bufferword);
                    if(candidateWord == keyword)
                    {
                        keywordFound = true;
                        position = charactersRead + 3;
                        break;
                    }
                }
            }

            if(keywordFound) return position;

            return -1;
        }

        public static string ReadSingleJsonAttribute(FileStream fileStream, ref int startPosition, bool fixUpJson = true)
        {
            StringBuilder finalString = new();
            if (fixUpJson) finalString = new("{\"");

            fileStream.Seek(startPosition, SeekOrigin.Begin);
            byte[] buffer = new byte[1];
            fileStream.Read(buffer, 0, 1);
            char character = (char)buffer[0];

            if(character == '}')
            {
                return "";
            }

            while(character != '"') {
                startPosition++;
                fileStream.Seek(startPosition, SeekOrigin.Begin);
                fileStream.Read(buffer, 0, 1);
                character = (char)buffer[0];
            }

            StreamReader reader = new StreamReader(fileStream);

            bool openingBracketEncountered = false;
            int openBracketCount = 0;
            int closedBracketCount = 0;
            while (!reader.EndOfStream)
            {
                character = (char)reader.Read();
                finalString.Append(character);

                if(character == '{')
                {
                    openingBracketEncountered = true;
                    openBracketCount++;
                }
                else if (character == '}')
                {
                    closedBracketCount++;
                }

                startPosition++;

                if (openingBracketEncountered && (closedBracketCount >= openBracketCount))
                {
                    break;
                }

            }

            if (fixUpJson) finalString.Append("}");

            return finalString.ToString();
        }

        private static string ReadPreviousChars(FileStream fs, long position, int count)
        {
            byte[] buffer = new byte[count];
            fs.Seek(position - count, SeekOrigin.Begin);
            fs.Read(buffer, 0, count);
            return Encoding.UTF8.GetString(buffer);
        }


    }
}
