﻿using LyricsScraper.Abstract;
using System.Net;
using System.Text.RegularExpressions;

namespace LyricsScraper.Genius
{
    public class GeniusParser: ILyricParser<string>
    {
        public string Parse(string lyric)
        {
            lyric = StripNewLines(lyric);
            lyric = StripTagsRegex(lyric);
            lyric = CleanEnding(lyric);
            lyric = WebUtility.HtmlDecode(lyric);

            return lyric;
        }

        public static string StripTagsRegex(string source)
        {
            return Regex.Replace(source, "<[^>]*>", string.Empty);
        }

        public static string StripNewLines(string source)
        {
            return Regex.Replace(source, @"(<br>|<br />|<br/>|</ br>|</br>)", "\r\n");
        }

        public string Urlify(string source)
        {
            return Regex.Replace(source, " ", "%20");
        }

        public static string CleanEnding(string source)
        {
            char[] charsToTrim = { '<', 'b', 'r', '>', ' ', '/' };
            for (int i = 0; i < 20; i++)
            {
                source = source.TrimEnd(charsToTrim);
            }
            return source;
        }
    }
}
