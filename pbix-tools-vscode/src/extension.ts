'use strict';
// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { PbixExplorerProvider } from './pbixExplorer';

// this method is called when your extension is activated
// your extension is activated the very first time the command is executed
export function activate(context: vscode.ExtensionContext) {

    // Use the console to output diagnostic information (console.log) and errors (console.error)
    // This line of code will only be executed once when your extension is activated
    console.log('Extension "pbix-tools" activated');

    const channel = vscode.window.createOutputChannel("pbix-tools");
    context.subscriptions.push(channel);

    const pbixExplorerProvider = new PbixExplorerProvider(context);
    vscode.window.registerTreeDataProvider('pbixExplorer', pbixExplorerProvider); // activation is determined in package.json/contributions

    // The command has been defined in the package.json file
    // Now provide the implementation of the command with  registerCommand
    // The commandId parameter must match the command field in package.json
    let disposable = vscode.commands.registerCommand('extension.sayHello', () => {
        // The code you place here will be executed every time your command is executed

        // Display a message box to the user
        vscode.window.showInformationMessage('Hello World!');
    });

    context.subscriptions.push(disposable);

    context.subscriptions.push(vscode.commands.registerCommand('pbixTools.openInPbixExplorer', (uri: vscode.Uri) => {
        vscode.window.showInformationMessage(`File clicked: ${uri.fsPath}`);
    }));

    context.subscriptions.push(vscode.commands.registerCommand('extension.openJsonSelection', () => {
        // TODO add args to command
        let o = {
            foo: "bar",
            x: [
                {
                    y: 1.2,
                    z: []
                }
            ]
        };
        vscode.workspace.openTextDocument({
            language: 'json',
            content: JSON.stringify(o, null, 2)
        }).then(doc => vscode.window.showTextDocument(doc));
    }));

    context.subscriptions.push(vscode.commands.registerCommand('extension.openXmlSelection', (name: string) => {
        // TODO add args to command
        vscode.workspace.openTextDocument({
            language: 'xml',
            content: `<root>
    <foo/>
        <bar name="${name}">
    </bar>
</root>`
        }).then(doc => vscode.window.showTextDocument(doc));
    }));
}

// this method is called when your extension is deactivated
export function deactivate() {
}