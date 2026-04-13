import Minus from '@lucide/svelte/icons/minus';
import Quote from '@lucide/svelte/icons/quote';
import SquareCode from '@lucide/svelte/icons/square-code';
import Puzzle from '@lucide/svelte/icons/puzzle';
import type { Editor } from '@tiptap/core';
import commands from '../../commands/toolbar-commands.ts';
import type { EdraToolBarCommands } from '../../commands/types.ts';
import strings from '../../strings.ts';

export interface Group {
	name: string;
	title: string;
	actions: EdraToolBarCommands[];
}

/** Additional component entries registered at runtime */
let _componentActions: EdraToolBarCommands[] = [];

export function registerComponentActions(actions: EdraToolBarCommands[]) {
	_componentActions = actions;
}

export function getGroups(): Group[] {
	const groups: Group[] = [
		{
			name: 'format',
			title: strings.command.formatGroup,
			actions: [
				...commands.headings,
				{
					icon: Quote,
					name: 'blockquote',
					tooltip: strings.command.blockQuote,
					onClick: (editor: Editor) => {
						editor.chain().focus().setBlockquote().run();
					}
				},
				{
					icon: SquareCode,
					name: 'codeBlock',
					tooltip: strings.command.codeBlock,
					onClick: (editor: Editor) => {
						editor.chain().focus().setCodeBlock().run();
					}
				},
				...commands.lists
			]
		},
		{
			name: 'insert',
			title: strings.command.insertGroup,
			actions: [
				...commands.media,
				...commands.table,
				{
					icon: Minus,
					name: 'horizontalRule',
					tooltip: strings.command.horizontalRule,
					onClick: (editor: Editor) => {
						editor.chain().focus().setHorizontalRule().run();
					}
				}
			]
		}
	];

	if (_componentActions.length > 0) {
		groups.push({
			name: 'components',
			title: 'Components',
			actions: _componentActions,
		});
	}

	return groups;
}

export const GROUPS = getGroups();
export { Puzzle as ComponentIcon };
export default GROUPS;
