using UnityEngine.UIElements;

namespace GraphProcessor
{
	public class ExposedParameterPropertyView : VisualElement
	{
		protected BaseGraphView baseGraphView;

		public ExposedParameter parameter { get; private set; }

		public Toggle     hideInInspector { get; private set; }
		
		public Toggle     isInput { get; private set; }

		public ExposedParameterPropertyView(BaseGraphView graphView, ExposedParameter param)
		{
			baseGraphView = graphView;
			parameter      = param;

			hideInInspector = new Toggle
			{
				text  = "Hide in Inspector",
				value = parameter.settings.isHidden
			};
			hideInInspector.RegisterValueChangedCallback(e =>
			{
				baseGraphView.graph.UpdateExposedParameterVisibility(parameter, e.newValue);
			});

			Add(hideInInspector);
			
			isInput = new Toggle
			{
				text  = "Is Input",
				value = parameter.settings.isInputParameter
			};
			isInput.RegisterValueChangedCallback(e =>
			{
				baseGraphView.graph.UpdateExposedParameterVisibility(parameter, e.newValue);
			});

			Add(isInput);
		}
	}
} 