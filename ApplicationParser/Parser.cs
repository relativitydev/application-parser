﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Xml;

namespace ApplicationParser
{
    public class Parser
    {
        public Application Parse(string xml, string overidePath = null)
        {
            var overrides = new HashSet<Guid>();
            if (File.Exists(overidePath))
            {
                overrides = GetOverrides(overidePath);
            }
            var xmlDoc = new XmlDocument(); // Create an XML document object
            xmlDoc.LoadXml(xml);
            var app = new Application();
            app.Guid = xmlDoc.SelectSingleNode("Application").SelectSingleNode("Guid").InnerText;
            app.Name = xmlDoc.SelectSingleNode("Application").SelectSingleNode("Name").InnerText;

            app.Objects = ParseObjects(xmlDoc, overrides);

            var tabList = new List<Tab>();
            var tabs = xmlDoc.GetElementsByTagName("Tab");
            foreach (XmlNode obj in tabs)
            {
                var objDef = ParseNode<Tab>(obj);
                tabList.Add(objDef);
            }
            app.Tabs = tabList;
            app.Scripts = ParseScripts(xmlDoc).ToList();
            return app;
        }


        private HashSet<Guid> GetOverrides(string path)
        {
            var content = File.ReadAllText(path);
            var xml = new XmlDocument();
            xml.LoadXml(content);
            var objs = xml.GetElementsByTagName("object");
            var hashSet = new HashSet<Guid>();
            foreach (XmlNode obj in objs)
            {
                var guid = obj.Attributes["guid"].Value;
                if (!Guid.TryParse(guid, out var g))
                {
                    continue;
                }
                var over = obj.Attributes["override"].Value;
                if (!bool.TryParse(over, out var shouldOverride))
                {
                    shouldOverride = false;
                }
                if (shouldOverride)
                {
                    hashSet.Add(g);
                }
            }
            return hashSet;
        }

        public IEnumerable<ObjectDef> ParseObjects(XmlDocument xmlDoc, HashSet<Guid> omOverrides)
        {
            var objects = xmlDoc.GetElementsByTagName("Object");
            foreach (XmlNode obj in objects)
            {
                var objDef = ParseObject(obj, omOverrides);
                yield return objDef;
            }
        }

        public IEnumerable<Script> ParseScripts(XmlDocument xmlDoc)
        {
            var scriptsList = new List<Script>();
            var scripts = xmlDoc.GetElementsByTagName("ApplicationScripts").Item(0).SelectNodes("ScriptElement");

            foreach (XmlNode obj in scripts)
            {
                var objDef = ParseNode<Script>(obj);
                yield return objDef;
            }
        }

        private ObjectDef ParseObject(XmlNode node, HashSet<Guid> omOverrides)
        {
            var obj = new ObjectDef();
            obj.Name = node.SelectSingleNode("Name").InnerText;
            obj.Guid = node.SelectSingleNode("Guid").InnerText;
            obj.ShouldUseOMModel = omOverrides.Contains(Guid.Parse(obj.Guid));

            var fields = node.SelectSingleNode("Fields").SelectNodes("Field");
            var systemFields = node.SelectSingleNode("SystemFields").SelectNodes("SystemField");
            var fieldList = new List<Field>();
            foreach (XmlNode field in fields)
            {
                var fieldDef = ParseField(field);
                if (fieldDef != null)
                {
                    fieldList.Add(fieldDef);
                }
            }
            foreach (XmlNode field in systemFields)
            {
                var fieldDef = ParseField(field, true);
                if (fieldDef != null)
                {
                    fieldList.Add(fieldDef);
                }
            }
            obj.Fields = fieldList;
            obj.ObjectRules = ParseObjectRules(node)?.ToList() ?? new List<ObjectRule>();
            obj.Layouts = ParseLayouts(node)?.ToList() ?? new List<Layout>();
            return obj;
        }

        private IEnumerable<ObjectRule> ParseObjectRules(XmlNode node)
        {
            var rules = new List<ObjectRule>();
            var rNodes = node.SelectSingleNode("ObjectRules");
            if (rNodes != null)
            {
                var oRules = rNodes.SelectNodes("ObjectRule");
                foreach (XmlNode oRule in oRules)
                {
                    var guid = oRule.SelectSingleNode("Guid");
                    var name = oRule.SelectSingleNode("Name");
                    rules.Add(new ObjectRule
                    {
                        Name = name.InnerText,
                        Guid = guid.InnerText
                    });

                }
            }
            return rules;
        }


        private IEnumerable<Layout> ParseLayouts(XmlNode node)
        {
            var layouts = new List<Layout>();
            var rNodes = node.SelectSingleNode("Layouts");
            if (rNodes != null)
            {
                var oRules = rNodes.SelectNodes("Layout");
                foreach (XmlNode oRule in oRules)
                {
                    var guid = oRule.SelectSingleNode("Guid");
                    var name = oRule.SelectSingleNode("Name");
                    layouts.Add(new Layout
                    {
                        Name = name.InnerText,
                        Guid = guid.InnerText
                    });

                }
            }
            return layouts;
        }


        private Field ParseField(XmlNode field, bool system = false)
        {
            var guid = field.SelectSingleNode("Guid");
            var name = field.SelectSingleNode("DisplayName");
            var fieldId = (FieldTypes)int.Parse(field.SelectSingleNode("FieldTypeId").InnerText);

            int.TryParse(field.SelectSingleNode("MaxLength")?.InnerText, out var length);

            var artifact = new Field
            {
                Guid = guid.InnerText,
                Name = name.InnerText,
                FieldType = fieldId,
                IsSystem = system,
                MaxLength = length
            };
            var choiceList = new List<ArtifactDef>();
            if (field.SelectNodes("Codes").Count > 0)
            {
                var codes = field.SelectSingleNode("Codes").SelectNodes("Code");
                foreach (XmlNode code in codes)
                {
                    var choiceDef = ParseNode<Field>(code);
                    choiceList.Add(choiceDef);
                }
            }
            artifact.Choices = choiceList;
            return artifact;
        }
        private T ParseNode<T>(XmlNode node) where T : ArtifactDef, new()
        {
            var guid = node.SelectNodes("Guid").Item(0);
            var name = node.SelectNodes("Name").Item(0);
            var artifact = new T { Guid = guid.InnerText, Name = name.InnerText };
            return artifact;
        }
    }
}
