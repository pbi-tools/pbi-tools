'use strict';
// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import * as fs from 'fs';
import * as child_process from 'child_process';
import { hrtime } from 'process';
// import { PbixExplorerProvider } from './pbixExplorer';

export function activate(context: vscode.ExtensionContext) {
    
    // Use the console to output diagnostic information (console.log) and errors (console.error)
    // This line of code will only be executed once when your extension is activated
    console.log('Extension "pbi-tools" activated');

    // TODO Check OS/CPU and disable extension if unsupported

    const channel = vscode.window.createOutputChannel("pbi-tools");
    context.subscriptions.push(channel);

    // const pbixExplorerProvider = new PbixExplorerProvider(context);
    // vscode.window.registerTreeDataProvider('pbixExplorer', pbixExplorerProvider); // activation is determined in package.json/contributions

    // // The command has been defined in the package.json file
    // // Now provide the implementation of the command with  registerCommand
    // // The commandId parameter must match the command field in package.json
    // let disposable = vscode.commands.registerCommand('extension.sayHello', () => {
    //     // The code you place here will be executed every time your command is executed

    //     // Display a message box to the user
    //     vscode.window.showInformationMessage('Hello World!');
    // });

    // context.subscriptions.push(disposable);

    context.subscriptions.push(vscode.commands.registerCommand('pbiTools.extractPbixHere', (uri: vscode.Uri) => {
        channel.clear();
        channel.show(true);
        // channel.appendLine(`File clicked: ${uri.fsPath}`);
        // locate pbix-tools.exe
        // invoke
        // redirect output to extension channel (open window)
        // channel.appendLine(`extensionPath: ${context.extensionPath}`);

        const exePath = context.asAbsolutePath('bin/pbi-tools.exe');

        if (fs.existsSync(exePath)) {
            const start = hrtime();
            const pbixPath = uri.fsPath;
            const pbixToolsProc = child_process.spawn(exePath, ['extract', pbixPath]);

            pbixToolsProc.stdout.on('data', (data) => {
                channel.append(`${data}`);
            });

            pbixToolsProc.stderr.on('data', (data) => {
                channel.append(`ERR: ${data}`);
            });

            pbixToolsProc.on('close', (code) => {
                const diff = hrtime(start);
                const elapsed = (diff[0] + diff[1] / 1e9).toFixed(2);
                channel.appendLine(`pbix-tools process exited with code ${code}. (Took ${elapsed} seconds)`);
            });

        }
        else {
            channel.appendLine(`'${exePath}' missing.`);
        }
    }));

//     context.subscriptions.push(vscode.commands.registerCommand('extension.openJsonSelection', () => {
//         // TODO add args to command
//         let o = {
//             foo: "bar",
//             x: [
//                 {
//                     y: 1.2,
//                     z: []
//                 }
//             ]
//         };
//         vscode.workspace.openTextDocument({
//             language: 'json',
//             content: JSON.stringify(o, null, 2)
//         }).then(doc => vscode.window.showTextDocument(doc));
//     }));

//     context.subscriptions.push(vscode.commands.registerCommand('extension.openXmlSelection', (name: string) => {
//         // TODO add args to command
//         vscode.workspace.openTextDocument({
//             language: 'xml',
//             content: `<root>
//     <foo/>
//         <bar name="${name}">
//     </bar>
// </root>`
//         }).then(doc => vscode.window.showTextDocument(doc));
//     }));
}

// this method is called when your extension is deactivated
export function deactivate() {
}