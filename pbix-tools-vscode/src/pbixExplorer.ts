import * as vscode from 'vscode';

export class PbixExplorerProvider implements vscode.TreeDataProvider<NodeBase> {

    //
    // Context: PbixToolsSession, open PBIX files

    constructor(private context: vscode.ExtensionContext) {
        // context.workspaceState;
    }

    // Interface: TreeDataProvider

    private _onDidChangeTreeData: vscode.EventEmitter<NodeBase> = new vscode.EventEmitter<NodeBase>();
    readonly onDidChangeTreeData: vscode.Event<NodeBase> = this._onDidChangeTreeData.event;
    
    getTreeItem(element: NodeBase): vscode.TreeItem | Thenable<vscode.TreeItem> {
        return element.getTreeItem();
        // // TODO Implement getTreeItem() on element type
        // if (element === PbixExplorerNodeType.GlobalInfo) {
        //     let item = new vscode.TreeItem("Info");
        //     item.tooltip = "Item tooltip";
        //     // TODO Command: show virtual document
        //     item.command = {
        //         command: 'extension.openJsonSelection',
        //         title: ''
        //     };
        //     return item;
        // }

        // let item = new vscode.TreeItem("Other");
        // item.tooltip = "Item tooltip";
        // return item;

        // (ROOT)
        // |- Info
        // |- {PBIX File Root} [Tooltip = path]
        //   |- Mashup
        //   |- Model
        //   |- ...
        // |- {PBIX File #2 Root}
        //   |-
    }

    getChildren(element?: NodeBase | undefined): vscode.ProviderResult<NodeBase[]> {
        if (!element) // return root node(s) if null
        {
            return this.getRootNodes();
        }
        return element.getChildren(element);
    }

    // end: TreeDataProvider


    private getRootNodes(): vscode.ProviderResult<NodeBase[]> {
        const rootNodes: RootNode[] = [];
        let node: RootNode;

        node = new RootNode('Images', 'imagesRootNode', this._onDidChangeTreeData);
        // this._imagesNode = node;
        rootNodes.push(node);

        node = new RootNode('Containers', 'containersRootNode', this._onDidChangeTreeData);
        // this._containersNode = node;
        rootNodes.push(node);

        node = new RootNode('Registries', 'registriesRootNode', this._onDidChangeTreeData);
        // this._registriesNode = node;
        rootNodes.push(node);

        return rootNodes;
    }

}

// also display PBIXPROJ folders from current workspace (diff icon?)
// PbixSession (specific file)

enum PbixExplorerNodeType {
    Root,
    GlobalInfo,
    FileRoot,
    Mashup,
    Model,
    ModelTable,
    ModelConnection,
    ModelMeasure,
    Report,
    Connections,
    Version,
    Metadata,
    Settings,
    DiagramViewState,
}

export class NodeBase {
    readonly label: string;

    protected constructor(label: string) {
        this.label = label;
    }

    getTreeItem(): vscode.TreeItem {
        return {
            label: this.label,
            collapsibleState: vscode.TreeItemCollapsibleState.None
        };
    }

    async getChildren(element: NodeBase): Promise<NodeBase[]> {
        return [];
    }
}

export class RootNode extends NodeBase
{
    constructor(
        public readonly label: string,
        public readonly contextValue: string,
        public eventEmitter: vscode.EventEmitter<NodeBase>
        // here pass in PbixServerContext
    ) {
        super(label);
    }

    getTreeItem(): vscode.TreeItem {
        return {
            label: this.label,
            collapsibleState: vscode.TreeItemCollapsibleState.None,
            contextValue: this.contextValue,   // this is important for command links
            command: { 
                command: 'extension.openXmlSelection', 
                title: '', 
                arguments: [this.contextValue]  
            }
            /* id, tooltip, resourceUri, iconPath */
        };
    }

    // TODO getChildren(element)
}


/* Node Types

Root: 
      ------------------------------
      SystemRoot
        System Info (open virtual Doc)
        Power BI Desktop Installations
          child node for each installation found
        MashupEngine Installations
      ------------------------------
      {ModelRoot} (file, folder)  -- backed by one Server proc & Server session (owns PbixModel)
        Project (PBIXPROJ.json)
        Mashup
          Parts (Package)
          Metadata (xml)
            Content (zip)
          Permissions
        Model
          Database (json)
          Data Sources
            {DataSource}
              Mashup
          Tables
            Measures
            Columns
            Hierarchies
            Partitions
          Relationships
          Expressions
        Report
          Document (json)
          Config (json)
          Filter (json)
          Pages
            PageX
              Visuals
          Metadata
          Settings
          DiagramState
          LinguisticSchema
        Resources
          Custom Visuals
          Static Resources
        Connections
        Version

    - Two alternatives: all logic in server, extension dumb -- OR -- view logic in ext only, server provides base data
    - Maintain only base types for nodes, provide all settings in server
      - Icon
      - ContextValue
      - Children
    - ext defines contextValue menu contribs

    - TODO - Find icons for all node types
           - Define node interactions (click, menu/context)
 */