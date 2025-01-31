﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using MdXaml.Plugins;

#if MIG_FREE
using Markdown.Xaml.LinkActions;
using MdStyle = Markdown.Xaml.MarkdownStyle;

namespace Markdown.Xaml
#else
using MdXaml.LinkActions;
using MdStyle = MdXaml.MarkdownStyle;

namespace MdXaml
#endif
{
    [ContentProperty(nameof(HereMarkdown))]
    public class MarkdownScrollViewer : FlowDocumentScrollViewer, IUriContext
    {
        public static readonly DependencyProperty SourceProperty =
             DependencyProperty.Register(
                 nameof(Source),
                 typeof(Uri),
                 typeof(MarkdownScrollViewer),
                 new PropertyMetadata(null, UpdateSource));

        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.Register(
                nameof(Markdown),
                typeof(string),
                typeof(MarkdownScrollViewer),
                new PropertyMetadata("", UpdateMarkdown));


        public static readonly DependencyProperty MarkdownStyleProperty =
            DependencyProperty.Register(
                nameof(MarkdownStyle),
                typeof(Style),
                typeof(MarkdownScrollViewer),
                new PropertyMetadata(MdStyle.Standard, UpdateStyle));

        public static readonly DependencyProperty MarkdownStyleNameProperty =
            DependencyProperty.Register(
            nameof(MarkdownStyleName),
            typeof(string),
            typeof(MarkdownScrollViewer),
            new PropertyMetadata(nameof(MdStyle.Standard), UpdateStyleName));

        public static readonly DependencyProperty AssetPathRootProperty =
            DependencyProperty.Register(
                nameof(AssetPathRoot),
                typeof(string),
                typeof(MarkdownScrollViewer),
                new PropertyMetadata(null, UpdateAssetPathRoot));


        private static void UpdateSource(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownScrollViewer owner && e.NewValue is Uri source)
            {
                owner.Open(source, false);
            }
        }

        private static void UpdateMarkdown(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownScrollViewer owner)
            {
                owner.Engine.Plugins = owner.Plugins;
                var doc = owner.Engine.Transform(owner.Markdown ?? "");
                owner.SetCurrentValue(DocumentProperty, doc);
            }
        }

        private static void UpdateStyle(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownScrollViewer owner)
            {
                owner.Engine.DocumentStyle = (Style)e.NewValue;
            }
        }

        private static void UpdateStyleName(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownScrollViewer owner)
            {
                var newName = (string)e.NewValue;

                if (newName == null) return;

                var prop = typeof(MarkdownStyle).GetProperty(newName);
                if (prop == null) return;

                owner.MarkdownStyle = (Style?)prop.GetValue(null);
            }
        }

        private static void UpdateAssetPathRoot(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownScrollViewer owner)
            {
                var newPath = (string)e.NewValue;
                var shouldUpdateMd = newPath != owner.Engine.AssetPathRoot;

                owner.Engine.AssetPathRoot = (string)e.NewValue;

                if (shouldUpdateMd) UpdateMarkdown(d, e);
            }
        }

        private Markdown _engine;
        public Markdown Engine
        {
            set
            {
                _engine = value;

                if (BaseUri != null)
                    _engine.BaseUri = BaseUri;

                if (AssetPathRoot != null)
                    _engine.AssetPathRoot = AssetPathRoot;

                if (MarkdownStyle != null)
                    _engine.DocumentStyle = MarkdownStyle;

                UpdateClickAction();
                UpdateMarkdown(this, default);
            }
            get => _engine;
        }

        private Uri? _baseUri;
        public Uri? BaseUri
        {
            set
            {
                _baseUri = value;
                Engine.BaseUri = value;
            }
            get => _baseUri ?? Engine.BaseUri;
        }

        public string AssetPathRoot
        {
            set => SetValue(AssetPathRootProperty, value);
            get => (string)GetValue(AssetPathRootProperty);
        }

        private MdXamlPlugins? _plugins;
#if MIG_FREE
        internal
#else
        public
#endif
        MdXamlPlugins? Plugins
        {
            set
            {
                _plugins = value;
                UpdateMarkdown(this, default);
            }
            get
            {
                if (_plugins is not null)
                    return _plugins;

                // load from application.resource
                var values = Application.Current?.Resources?.Values;
                if (values is null) return null;

                return _plugins = values.OfType<MdXamlPlugins>().FirstOrDefault();
            }
        }


        public string HereMarkdown
        {
            get { return Markdown; }
            set
            {
                if (String.IsNullOrEmpty(value))
                {
                    Markdown = value;
                }
                else
                {
                    // like PHP's flexible_heredoc_nowdoc_syntaxes,
                    // The indentation of the closing tag dictates 
                    // the amount of whitespace to strip from each line 
                    var lines = Regex.Split(value, "\r\n|\r|\n", RegexOptions.Multiline);

                    // count last line indent
                    int lastIdtCnt = TextUtil.CountIndent(lines.Last());
                    // count full indent
                    int someIdtCnt = lines
                        .Where(line => !String.IsNullOrWhiteSpace(line))
                        .Select(line => TextUtil.CountIndent(line))
                        .Min();

                    var indentCount = Math.Max(lastIdtCnt, someIdtCnt);

                    Markdown = String.Join(
                        "\n",
                        lines
                            // skip first blank line
                            .Skip(String.IsNullOrWhiteSpace(lines[0]) ? 1 : 0)
                            // strip indent
                            .Select(line =>
                            {
                                var realIdx = 0;
                                var viewIdx = 0;

                                while (viewIdx < indentCount && realIdx < line.Length)
                                {
                                    var c = line[realIdx];
                                    if (c == ' ')
                                    {
                                        realIdx += 1;
                                        viewIdx += 1;
                                    }
                                    else if (c == '\t')
                                    {
                                        realIdx += 1;
                                        viewIdx = ((viewIdx >> 2) + 1) << 2;
                                    }
                                    else break;
                                }

                                return line.Substring(realIdx);
                            })
                        );
                }
            }
        }

        public string Markdown
        {
            get { return (string)GetValue(MarkdownProperty); }
            set { SetValue(MarkdownProperty, value); }
        }

        public Style? MarkdownStyle
        {
            get { return (Style)GetValue(MarkdownStyleProperty); }
            set { SetValue(MarkdownStyleProperty, value); }
        }

        public string MarkdownStyleName
        {
            get { return (string)GetValue(MarkdownStyleNameProperty); }
            set { SetValue(MarkdownStyleNameProperty, value); }
        }

        public Uri? Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        private ClickAction _clickAction;
        public ClickAction ClickAction
        {
            get { return _clickAction; }
            set
            {
                _clickAction = value;
                UpdateClickAction();
                UpdateMarkdown(this, default);
            }
        }

        public MarkdownScrollViewer()
        {
            _engine = new Markdown();

            if (BaseUri != null)
                _engine.BaseUri = BaseUri;

            if (AssetPathRoot != null)
                _engine.AssetPathRoot = AssetPathRoot;

            if (MarkdownStyle != null)
                _engine.DocumentStyle = MarkdownStyle;

            UpdateClickAction();
        }

        private void UpdateClickAction()
        {
            switch (_clickAction)
            {
                case ClickAction.OpenBrowser:
                    Engine.HyperlinkCommand = new OpenCommand();
                    break;

                case ClickAction.DisplayWithRelativePath:
                    Engine.HyperlinkCommand = new DiaplayCommand(this, true);
                    break;

                case ClickAction.DisplayAll:
                    Engine.HyperlinkCommand = new DiaplayCommand(this, false);
                    break;
            }
        }

        internal void Open(Uri source, bool updateSourceProperty)
        {
            bool TryOpen(Uri path)
            {
                try
                {
                    string newMdTxt;

                    switch (path.Scheme)
                    {
                        case "http":
                        case "https":
                            using (var wc = new WebClient())
                            using (var strm = new MemoryStream(wc.DownloadData(path)))
                            using (var reader = new StreamReader(strm, true))
                                newMdTxt = reader.ReadToEnd();
                            break;

                        case "file":
                            using (var strm = File.OpenRead(path.LocalPath))
                            using (var reader = new StreamReader(strm, true))
                                newMdTxt = reader.ReadToEnd();
                            break;

                        case "pack":
                            using (var strm = Application.GetResourceStream(path).Stream)
                            using (var reader = new StreamReader(strm, true))
                                newMdTxt = reader.ReadToEnd();
                            break;

                        default:
                            throw new ArgumentException($"unsupport schema {path.Scheme}");
                    }

                    var assetPathRoot = path.Scheme == "file" ? Path.GetDirectoryName(path.LocalPath) : path.AbsoluteUri;

                    Engine.AssetPathRoot = assetPathRoot;

                    SetCurrentValue(AssetPathRootProperty, assetPathRoot);
                    SetCurrentValue(MarkdownProperty, newMdTxt);

                    if (updateSourceProperty)
                        SetCurrentValue(SourceProperty, path);

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            if (source.IsAbsoluteUri)
            {
                TryOpen(source);
            }
            else if (BaseUri is null)
            {
                Debug.WriteLine($"Failed to open markdown from relative path '{source}': BaseUri is null");
            }
            else if (!TryOpen(new Uri(BaseUri, source)))
            {
                if (Uri.IsWellFormedUriString(AssetPathRoot, UriKind.Absolute))
                {
                    var assetUri = new Uri(new Uri(AssetPathRoot), source);
                    TryOpen(assetUri);
                }
                else
                {
                    var assetPath = Path.Combine(AssetPathRoot, source.ToString());
                    TryOpen(new Uri(assetPath));
                }
            }
            else
            {
                Debug.WriteLine($"Failed to open markdown from relative path '{source}': not found");
            }
        }
    }

    public enum ClickAction
    {
        None,
        OpenBrowser,
        DisplayWithRelativePath,
        DisplayAll
    }
}
