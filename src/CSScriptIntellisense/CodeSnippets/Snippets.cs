using Intellisense.Common;
using Kbg.NppPluginNET.PluginInfrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CSScriptIntellisense
{
    public class SnippetContext
    {
        public static int indicatorId = 8;
        public List<List<Point>> ParametersGroups = new List<List<Point>>();
        public List<Point> Parameters = new List<Point>();
        public Point? CurrentParameter;
        public string CurrentParameterValue = "";
        public string ReplacementString = "";
    }

    public class SnippetCompletionData : ICompletionData
    {
        public SnippetCompletionData()
        {
            CompletionType = CompletionType.snippet;
        }

        public CompletionCategory CompletionCategory { get; set; }
        public string CompletionText { get; set; }
        public string Description { get; set; }
        public CompletionType CompletionType { get; set; }
        public DisplayFlags DisplayFlags { get; set; }
        public string DisplayText { get; set; }
        public bool InvokeParametersSet { get; set; }
        public string OperationContext { get; set; }
        public object Tag { get; set; }

        public bool HasOverloads
        {
            get { return false; }
        }

        public void AddOverload(ICompletionData data)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ICompletionData> OverloadedData
        {
            get { throw new NotImplementedException(); }
        }

        public string InvokeReturn { get; set; }

        public IEnumerable<string> InvokeParameters
        {
            get { throw new NotImplementedException(); }
        }
    }

    public class Snippets
    {
        public static SnippetContext CurrentContext = null;

        static public Dictionary<string, string> Map = new Dictionary<string, string>();

        static public IEnumerable<string> Keys
        {
            get
            {
                return Snippets.Map.Keys;
            }
        }

        static public void ReplaceTextAtIndicator(string text, Point indicatorRange)
        {
            var document = Npp.GetCurrentDocument();
            document.SetTextBetween(text, indicatorRange);

            //restore the indicator
            document.SetIndicatorStyle(SnippetContext.indicatorId, SciMsg.INDIC_BOX, Color.Blue);
            document.PlaceIndicator(SnippetContext.indicatorId, indicatorRange.X, indicatorRange.X + text.Length);
        }

        static public bool ScintillaIndicatorDiscoveryIsBroken = true;

        static public void FinalizeCurrent()
        {
            var document = Npp.GetCurrentDocument();
            var indicators = document.FindIndicatorRanges(SnippetContext.indicatorId);
            if (ScintillaIndicatorDiscoveryIsBroken && !indicators.Any())
                indicators = CurrentContext.Parameters.ToArray();

            foreach (var range in indicators)
                document.ClearIndicator(SnippetContext.indicatorId, range.X, range.Y);

            var caretPoint = indicators.Where(point =>
                                              {
                                                  string text = document.GetTextBetween(point);
                                                  return text == " " || text == "|";
                                              })
                                       .FirstOrDefault();

            if (caretPoint.X != caretPoint.Y)
            {
                document.SetTextBetween("", caretPoint);
                document.SetSelection(caretPoint.X, caretPoint.X);
            }
        }

        static public bool NavigateToNextParam(SnippetContext context)
        {
            var document = Npp.GetCurrentDocument();

            var indicators = document.FindIndicatorRanges(SnippetContext.indicatorId);

            if (!indicators.Any())
                return false;

            Point currentParam = context.CurrentParameter.Value;
            string currentParamOriginalText = context.CurrentParameterValue;

            document.SetSelection(currentParam.X, currentParam.X);
            string currentParamDetectedText = document.GetWordAtCursor("\t\n\r ,;'\"".ToCharArray());

            if (currentParamOriginalText != currentParamDetectedText)
            {
                //current parameter is modified, indicator is destroyed so restore the indicator first
                document.SetIndicatorStyle(SnippetContext.indicatorId, SciMsg.INDIC_BOX, Color.Blue);
                document.PlaceIndicator(SnippetContext.indicatorId, currentParam.X, currentParam.X + currentParamDetectedText.Length);

                indicators = document.FindIndicatorRanges(SnippetContext.indicatorId);//needs refreshing as the document is modified

                var paramsInfo = indicators.Select(p =>
                                                   new
                                                   {
                                                       Index = indicators.IndexOf(p),
                                                       Text = document.GetTextBetween(p),
                                                       Range = p,
                                                       Pos = p.X
                                                   })
                                           .OrderBy(x => x.Pos)
                                           .ToArray();

                var paramsToUpdate = paramsInfo.Where(item => item.Text == currentParamOriginalText).ToArray();

                foreach (var param in paramsToUpdate)
                {
                    Snippets.ReplaceTextAtIndicator(currentParamDetectedText, indicators[param.Index]);
                    indicators = document.FindIndicatorRanges(SnippetContext.indicatorId);//needs refreshing as the document is modified
                }
            }

            Point? nextParameter = null;

            int currentParamIndex = indicators.FindIndex(x => x.X >= currentParam.X); //can also be logical 'next'
            var prevParamsValues = indicators.Take(currentParamIndex).Select(p => document.GetTextBetween(p)).ToList();
            prevParamsValues.Add(currentParamOriginalText);
            prevParamsValues.Add(currentParamDetectedText);
            prevParamsValues.Add(" ");
            prevParamsValues.Add("|");

            foreach (var range in indicators.ToArray())
            {
                if (currentParam.X < range.X && !prevParamsValues.Contains(document.GetTextBetween(range)))
                {
                    nextParameter = range;
                    break;
                }
            }

            if (!nextParameter.HasValue)
                nextParameter = indicators.FirstOrDefault();

            context.CurrentParameter = nextParameter;
            if (context.CurrentParameter.HasValue)
            {
                document.SetSelection(context.CurrentParameter.Value.X, context.CurrentParameter.Value.Y);
                context.CurrentParameterValue = document.GetTextBetween(context.CurrentParameter.Value);
            }

            return true;
        }

        public static bool Contains(string snippetTag)
        {
            lock (Map)
            {
                return Map.ContainsKey(snippetTag);
            }
        }

        public static void Init()
        {
            lock (Map)
            {
                if (!File.Exists(ConfigFile))
                    File.WriteAllText(ConfigFile, global::CSScriptIntellisense.CodeSnippets.Resources.snippets);
                Read(ConfigFile);
                SetupFileWatcher();
            }
        }

        static void SetupFileWatcher()
        {
            string dir = Path.GetDirectoryName(ConfigFile);
            string fileName = Path.GetFileName(ConfigFile);
            configWatcher = new FileSystemWatcher(dir, fileName);
            configWatcher.NotifyFilter = NotifyFilters.LastWrite;
            configWatcher.Changed += configWatcher_Changed;
            configWatcher.EnableRaisingEvents = true;
        }

        public static string GetTemplate(string snippetTag)
        {
            lock (Map)
            {
                if (Map.ContainsKey(snippetTag))
                    return Map[snippetTag];
                else
                    return null;
            }
        }

        static string ConfigFile
        {
            get
            {
                string configDir = Path.Combine(Npp.Editor.GetPluginsConfigDir(), "CSharpIntellisense");

                if (!Directory.Exists(configDir))
                    Directory.CreateDirectory(configDir);

                return Path.Combine(configDir, "snippet.data");
            }
        }

        static FileSystemWatcher configWatcher;

        static public void EditSnippetsConfig()
        {
            Npp.Editor.Open(Snippets.ConfigFile);
        }

        static void configWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            Init();
        }

        static void Read(string file)
        {
            //Debug.Assert(false);
            Map.Clear();
            try
            {
                var buffer = new StringBuilder();
                var currentTag = "";

                foreach (var line in File.ReadAllLines(file))
                {
                    if (line.StartsWith("#"))
                        continue; //comment line

                    if (line.EndsWith("=>") && !line.StartsWith(" "))
                    {
                        if (currentTag != "")
                        {
                            Map.Add(currentTag, buffer.ToString());
                            buffer.Clear();
                        }

                        currentTag = line.Replace("=>", "").Trim();
                    }
                    else
                        buffer.AppendLine(line);
                }

                if (currentTag != "")
                    Map.Add(currentTag, buffer.ToString());
            }
            catch (Exception e)
            {
                MessageBox.Show("Cannot load code Snippets.\n" + e.Message, "CS-Script");
            }
        }

        public static SnippetContext PrepareForIncertion(string rawText, int charsOffset, int documentOffset = 0)
        {
            var retval = new SnippetContext();

            retval.ReplacementString = rawText;

            string offset = new string(' ', charsOffset);
            retval.ReplacementString = retval.ReplacementString.Replace(Environment.NewLine, Environment.NewLine + offset);

            int endPos = -1;
            int startPos = retval.ReplacementString.IndexOf("$");

            while (startPos != -1)
            {
                endPos = retval.ReplacementString.IndexOf("$", startPos + 1);

                if (endPos != -1)
                {
                    //'$item$' -> 'item'
                    int newEndPos = endPos - 2;

                    // Scintilla indicator discovery is broken so we cannot navigate replaceable params
                    // so only add the final cursor param position
                    if (!ScintillaIndicatorDiscoveryIsBroken || retval.ReplacementString.Substring(startPos, 3) == "$|$")
                        retval.Parameters.Add(new Point(startPos + documentOffset, newEndPos + 1 + documentOffset));

                    string leftText = retval.ReplacementString.Substring(0, startPos);
                    string rightText = retval.ReplacementString.Substring(endPos + 1);
                    string placementValue = retval.ReplacementString.Substring(startPos + 1, endPos - startPos - 1);

                    retval.ReplacementString = leftText + placementValue + rightText;

                    endPos = newEndPos;
                }
                else
                    break;

                startPos = retval.ReplacementString.IndexOf("$", endPos + 1);
            }

            if (retval.Parameters.Any())
                retval.CurrentParameter = retval.Parameters.FirstOrDefault();

            return retval;
        }
    }

#if DEBUG

    public class ActiveDev
    {
        static public void SetMarker()
        {
            IntPtr sci = PluginBase.GetCurrentScintilla();
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 0, (int)SciMsg.SC_MARK_CIRCLE);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 1, (int)SciMsg.SC_MARK_ROUNDRECT);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 2, (int)SciMsg.SC_MARK_ARROW);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 3, (int)SciMsg.SC_MARK_SMALLRECT);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 4, (int)SciMsg.SC_MARK_SHORTARROW);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 5, (int)SciMsg.SC_MARK_EMPTY);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 6, (int)SciMsg.SC_MARK_ARROWDOWN);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 7, (int)SciMsg.SC_MARK_MINUS);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 8, (int)SciMsg.SC_MARK_PLUS);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 9, (int)SciMsg.SC_MARK_ARROWS);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 10, (int)SciMsg.SC_MARK_DOTDOTDOT);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 11, (int)SciMsg.SC_MARK_BACKGROUND);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 12, (int)SciMsg.SC_MARK_LEFTRECT);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 13, (int)SciMsg.SC_MARK_FULLRECT);
            Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, 14, (int)SciMsg.SC_MARK_UNDERLINE);

            for (int i = 15; i <= 32; i++)
                Win32.SendMessage(sci, SciMsg.SCI_MARKERDEFINE, i, (int)SciMsg.SC_MARK_CHARACTER + i + 50);

            //'add 1 marker to each line
            for (int i = 0; i <= 32; i++)
            {
                Win32.SendMessage(sci, SciMsg.SCI_SETMARGINMASKN, 1, -1);//  'all symbols allowed
                Win32.SendMessage(sci, SciMsg.SCI_MARKERADD, i, i);       //'line, marker#
            }

            //'set background of background marker to red
            //SendMessage hSci, %SCI_MarkerSetBack, 11, Rgb(220,220,220)   'gray background
        }

        public static void Style()
        {
            IntPtr sci = PluginBase.GetCurrentScintilla();

            Win32.SendMessage(sci, SciMsg.SCI_INDICSETSTYLE, 8, (int)SciMsg.INDIC_PLAIN);
            Win32.SendMessage(sci, SciMsg.SCI_INDICSETSTYLE, 9, (int)SciMsg.INDIC_SQUIGGLE);
            Win32.SendMessage(sci, SciMsg.SCI_INDICSETSTYLE, 10, (int)SciMsg.INDIC_TT);
            Win32.SendMessage(sci, SciMsg.SCI_INDICSETSTYLE, 11, (int)SciMsg.INDIC_DIAGONAL);
            Win32.SendMessage(sci, SciMsg.SCI_INDICSETSTYLE, 12, (int)SciMsg.INDIC_STRIKE);
            Win32.SendMessage(sci, SciMsg.SCI_INDICSETSTYLE, 13, (int)SciMsg.INDIC_BOX);
            //Win32.SendMessage(sci, SciMsg.SCI_INDICSETSTYLE, 14, (int)SciMsg.INDIC_ROUNDBOX);
            Win32.SendMessage(sci, SciMsg.SCI_INDICSETSTYLE, 14, (int)SciMsg.INDIC_CONTAINER);

            for (int i = 8; i <= 14; i++)
            {
                Win32.SendMessage(sci, SciMsg.SCI_SETINDICATORCURRENT, i, 0);
                Win32.SendMessage(sci, SciMsg.SCI_INDICSETFORE, i, 0x0000ff);
                int iStart = (int)Win32.SendMessage(sci, SciMsg.SCI_POSITIONFROMLINE, i - 8, 0);
                Win32.SendMessage(sci, SciMsg.SCI_INDICATORFILLRANGE, iStart, 7);
            }
        }

        public static void Unstyle()
        {
            IntPtr sci = PluginBase.GetCurrentScintilla();

            for (int i = 8; i <= 14; i++)
            {
                //for (int i = 0; i < length; i++)
                //{
                //}

                Win32.SendMessage(sci, SciMsg.SCI_SETINDICATORCURRENT, i, 0);

                //finding the indicator ranges
                //For example indicator 4..6 in the doc 0..10 will have three logical regions:
                //0..4, 4..6, 6..10
                //Probing will produce following when outcome:
                //probe for 0 : 0..4
                //probe for 4 : 4..6
                //probe for 6 : 4..10
                for (int j = 0; j < 500; j++)
                {
                    int iS = (int)Win32.SendMessage(sci, SciMsg.SCI_INDICATORSTART, i, j);
                    int iE = (int)Win32.SendMessage(sci, SciMsg.SCI_INDICATOREND, i, j);
                    Debug.WriteLine("indicator {0}; Test position {1}; iStart: {2}; iEnd: {3};", i, j, iS, iE);
                }

                //finding indicator presence within a range (by probing the range position)
                //For example indicator 4..6 in the doc 0..10 will have three logical regions:
                //0..4, 5..6, 6..10
                //probe for 3 -> 0
                //probe for 4 -> 1
                //probe for 7 -> 0
                for (int j = 0; j < 500; j++)
                {
                    int value = (int)Win32.SendMessage(sci, SciMsg.SCI_INDICATORVALUEAT, i, j);
                    //Debug.WriteLine("indicator {0}; Test position {1}; iStart: {2}; iEnd: {3};", i, j, iS, iE);
                }

                int lStart = (int)Win32.SendMessage(sci, SciMsg.SCI_POSITIONFROMLINE, i - 8, 0);
                int iStart = (int)Win32.SendMessage(sci, SciMsg.SCI_INDICATORSTART, lStart, lStart + 50);
                int iEnd = (int)Win32.SendMessage(sci, SciMsg.SCI_INDICATOREND, lStart, lStart + 50);
                Win32.SendMessage(sci, SciMsg.SCI_INDICATORCLEARRANGE, iStart, iEnd - iStart);

                //int iStart = (int)Win32.SendMessage(sci, SciMsg.SCI_POSITIONFROMLINE, i - 8, 0);
                //Win32.SendMessage(sci, SciMsg.SCI_INDICATORCLEARRANGE, iStart, 7);
            }
        }
    }

#endif
}