using System.Xml.Linq;
using EZEditor.ViewModels;

namespace EZEditor.Services;

public sealed record XmlParseResult(XDocument Document, XmlNodeViewModel Root);
