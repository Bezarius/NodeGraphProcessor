﻿using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System.Reflection;
using System;
using System.Linq;
using UnityEditorInternal;

using Status = UnityEngine.UIElements.DropdownMenuAction.Status;
using NodeView = UnityEditor.Experimental.GraphView.Node;

namespace GraphProcessor
{
	[NodeCustomEditor(typeof(BaseNode))]
	public class BaseNodeView : NodeView
	{
		public BaseNode							nodeTarget;

		public List< Port >						inputPorts = new List< Port >();
		public List< Port >						outputPorts = new List< Port >();

		public BaseGraphView					owner { private set; get; }

		protected Dictionary< string, List< PortView > > portsPerFieldName = new Dictionary< string, List< PortView > >();

        protected VisualElement 				controlsContainer;
		protected VisualElement					debugContainer;

		Label									computeOrderLabel = new Label();

		public event Action< PortView >			onPortConnected;
		public event Action< PortView >			onPortDisconnected;

		readonly string							baseNodeStyle = "GraphProcessorStyles/BaseNodeView";

		#region  Initialization

		public void Initialize(BaseGraphView owner, BaseNode node)
		{
			nodeTarget = node;
			this.owner = owner;

			owner.computeOrderUpdated += ComputeOrderUpdatedCallback;

			styleSheets.Add(Resources.Load<StyleSheet>(baseNodeStyle));

			InitializePorts();
			InitializeView();
			InitializeDebug();

			Enable();

			this.RefreshPorts();
		}

		void InitializePorts()
		{
			var listener = owner.connectorListener;

			foreach (var fieldInfoKP in nodeTarget.nodeFields)
			{
				var fieldInfo = fieldInfoKP.Value;
				var direction = (fieldInfo.input) ? Direction.Input : Direction.Output;

				if (fieldInfo.behavior != null)
				{
					UpdateNodePortsForField(fieldInfo);
				}
				else
				{
					AddPort(fieldInfo.info, direction, listener, fieldInfo.isMultiple, new PortData { displayName = fieldInfo.name });
				}
			}
		}

		void UpdateNodePortsForField(BaseNode.NodeFieldInformation fieldInfo)
		{
			if (fieldInfo.behavior == null)
				return ;

			#if true

			List< string > finalPorts = new List< string >();
			var currentPorts = GetPortViewsFromFieldName(fieldInfo.fieldName);
			var listener = owner.connectorListener;
			var direction = fieldInfo.input ? Direction.Input : Direction.Output;
			var container = fieldInfo.input ? nodeTarget.inputPorts as NodePortContainer : nodeTarget.outputPorts as NodePortContainer;
			var nodePort = container.FirstOrDefault(np => np.fieldName == fieldInfo.fieldName);

			foreach (var portData in fieldInfo.behavior(nodePort.GetEdges()))
			{
				// Add only ports that are not currently here
				if (currentPorts == null || !currentPorts.Any(p => p.identifier == portData.identifier))
					AddPort(fieldInfo.info, direction, listener, fieldInfo.isMultiple, portData);
				else
				{
					// TODO: patch the name of the ports

				}
				finalPorts.Add(portData.identifier);
			}

			// Remove only the ports that are no more in the list
			if (currentPorts != null)
			{
				var currentPortsCopy = currentPorts.ToList();
				foreach (var currentPort in currentPortsCopy)
				{
					// If the current port does not appear in the list of final ports, we remove it
					if (!finalPorts.Any(id => id == currentPort.identifier))
					{
						RemovePort(currentPort);
					}
				}
			}

			#else

			var currentPorts = GetPortViewsFromFieldName(fieldInfo.fieldName);
			if (currentPorts != null)
			{
				var currentPortsCopy = currentPorts.ToList();
				currentPortsCopy.ForEach(p => RemovePort(p));
			}

			var listener = owner.connectorListener;
			var direction = fieldInfo.input ? Direction.Input : Direction.Output;
			var container = fieldInfo.input ? nodeTarget.inputPorts as NodePortContainer : nodeTarget.outputPorts as NodePortContainer;
			var nodePort = container.FirstOrDefault(np => np.fieldName == fieldInfo.fieldName);

			foreach (var portData in fieldInfo.behavior(nodePort.GetEdges()))
			{
				AddPort(fieldInfo.info, direction, listener, fieldInfo.isMultiple, portData);
			}

			#endif
		}

		void InitializeView()
		{
            controlsContainer = new VisualElement{ name = "controls" };
			mainContainer.Add(controlsContainer);

			debugContainer = new VisualElement{ name = "debug" };
			if (nodeTarget.debug)
				mainContainer.Add(debugContainer);

			title = (string.IsNullOrEmpty(nodeTarget.name)) ? nodeTarget.GetType().Name : nodeTarget.name;

			SetPosition(nodeTarget.position);
		}

		void InitializeDebug()
		{
			ComputeOrderUpdatedCallback();
			debugContainer.Add(computeOrderLabel);
		}

		#endregion

		#region API

		public List< PortView > GetPortViewsFromFieldName(string fieldName)
		{
			List< PortView >	ret;

			portsPerFieldName.TryGetValue(fieldName, out ret);

			return ret;
		}

		public PortView GetFirstPortViewFromFieldName(string fieldName)
		{
			return GetPortViewsFromFieldName(fieldName)?.First();
		}

		public PortView GetPortViewFromFieldName(string fieldName, string identifier)
		{
			return GetPortViewsFromFieldName(fieldName)?.FirstOrDefault(pv => {
				return (pv.identifier == identifier) || (String.IsNullOrEmpty(pv.identifier) && String.IsNullOrEmpty(identifier));
			});
		}

		public PortView AddPort(FieldInfo fieldInfo, Direction direction, EdgeConnectorListener listener, bool isMultiple = false, PortData portData = null)
		{
			// TODO: hardcoded value
			PortView p = new PortView(Orientation.Horizontal, direction, fieldInfo, portData?.displayType, listener, portData?.identifier);

			if (p.direction == Direction.Input)
			{
				inputPorts.Add(p);
				inputContainer.Add(p);
			}
			else
			{
				outputPorts.Add(p);
				outputContainer.Add(p);
			}

			p.Initialize(this, isMultiple, portData?.displayName);

			List< PortView > ports;
			portsPerFieldName.TryGetValue(p.fieldName, out ports);
			if (ports == null)
			{
				ports = new List< PortView >();
				portsPerFieldName[p.fieldName] = ports;
			}
			ports.Add(p);

			return p;
		}

		public void RemovePort(PortView p)
		{
			if (p.direction == Direction.Input)
			{
				inputPorts.Remove(p);
				inputContainer.Remove(p);
			}
			else
			{
				outputPorts.Remove(p);
				outputContainer.Remove(p);
			}

			List< PortView > ports;
			portsPerFieldName.TryGetValue(p.fieldName, out ports);
			ports.Remove(p);
		}

		public void OpenNodeViewScript()
		{
			var scriptPath = NodeProvider.GetNodeViewScript(GetType());

			if (scriptPath != null)
				InternalEditorUtility.OpenFileAtLineExternal(scriptPath, 0);
		}

		public void OpenNodeScript()
		{
			var scriptPath = NodeProvider.GetNodeScript(nodeTarget.GetType());

			if (scriptPath != null)
				InternalEditorUtility.OpenFileAtLineExternal(scriptPath, 0);
		}

		public void ToggleDebug()
		{
			nodeTarget.debug = !nodeTarget.debug;
			UpdateDebugView();
		}

		public void UpdateDebugView()
		{
			if (nodeTarget.debug)
				mainContainer.Add(debugContainer);
			else
				mainContainer.Remove(debugContainer);
		}

		#endregion

		#region Callbacks & Overrides

		void ComputeOrderUpdatedCallback()
		{
			//Update debug compute order
			computeOrderLabel.text = "Compute order: " + nodeTarget.computeOrder;
		}

		public virtual void Enable()
		{
			DrawDefaultInspector();
		}

		public virtual void DrawDefaultInspector()
		{
			var fields = nodeTarget.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

			foreach (var field in fields)
			{
				//skip if the field is not serializable
				if (!field.IsPublic && field.GetCustomAttribute(typeof(SerializeField)) == null)
					continue ;

				//skip if the field is an input/output and not marked as SerializedField
				if (field.GetCustomAttribute(typeof(SerializeField)) == null && (field.GetCustomAttribute(typeof(InputAttribute)) != null || field.GetCustomAttribute(typeof(OutputAttribute)) != null))
					continue ;

                //skip if marked with NonSerialized or HideInInspector
                if (field.GetCustomAttribute(typeof(System.NonSerializedAttribute)) != null || field.GetCustomAttribute(typeof(HideInInspector)) != null)
                    continue ;

				var controlLabel = new Label(field.Name);
                controlsContainer.Add(controlLabel);

				var element = FieldFactory.CreateField(field.FieldType, field.GetValue(nodeTarget), (newValue) => {
					owner.RegisterCompleteObjectUndo("Updated " + newValue);
					field.SetValue(nodeTarget, newValue);
				});

				if (element != null)
					controlsContainer.Add(element);
			}
		}

		public void OnPortConnected(PortView port)
		{
			var nodeField = nodeTarget.nodeFields.FirstOrDefault(n => n.Value.fieldName == port.fieldName).Value;

			UpdateNodePortsForField(nodeField);
			onPortConnected?.Invoke(port);
		}

		public void OnPortDisconnected(PortView port)
		{
			var nodeField = nodeTarget.nodeFields.FirstOrDefault(n => n.Value.fieldName == port.fieldName).Value;

			UpdateNodePortsForField(nodeField);
			onPortDisconnected?.Invoke(port);
		}

		// TODO: a function to force to reload the custom behavior ports (if we want to do a button to add ports for example)

		public virtual void OnRemoved() {}
		public virtual void OnCreated() {}

		public override void SetPosition(Rect newPos)
		{
			base.SetPosition(newPos);

			Undo.RegisterCompleteObjectUndo(owner.graph, "Moved graph node");
			nodeTarget.position = newPos;
		}

		public override bool	expanded
		{
			get { return base.expanded; }
			set
			{
				base.expanded = value;
				nodeTarget.expanded = value;
			}
		}

		public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
		{
			evt.menu.AppendAction("Open Node Script", (e) => OpenNodeScript(), OpenNodeScriptStatus);
			evt.menu.AppendAction("Open Node View Script", (e) => OpenNodeViewScript(), OpenNodeViewScriptStatus);
			evt.menu.AppendAction("Debug", (e) => ToggleDebug(), DebugStatus);
		}

		Status DebugStatus(DropdownMenuAction action)
		{
			if (nodeTarget.debug)
				return Status.Checked;
			return Status.Normal;
		}

		Status OpenNodeScriptStatus(DropdownMenuAction action)
		{
			if (NodeProvider.GetNodeScript(nodeTarget.GetType()) != null)
				return Status.Normal;
			return Status.Disabled;
		}

		Status OpenNodeViewScriptStatus(DropdownMenuAction action)
		{
			if (NodeProvider.GetNodeViewScript(GetType()) != null)
				return Status.Normal;
			return Status.Disabled;
		}

		#endregion
    }
}