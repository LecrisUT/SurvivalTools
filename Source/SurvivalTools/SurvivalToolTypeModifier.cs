using RimWorld;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace SurvivalTools
{
    public class SurvivalToolTypeModifier
    {
        public SurvivalToolType toolType;
        public float value;
		public void LoadDataFromXmlCustom(XmlNode xmlRoot)
		{
			DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "toolType", xmlRoot.Name);
			value = ParseHelper.FromString<float>(xmlRoot.FirstChild.Value);
		}

		public override string ToString()
		{
			if (toolType == null)
			{
				return "(null toolType)";
			}
			return toolType.defName + "-" + value.ToString();
		}
	}
}
