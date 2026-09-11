import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

interface OpenList {
  tag: 'ul' | 'ol';
  indent: number;
  itemOpen: boolean;
}

@Pipe({
  name: 'chatMarkdown',
  standalone: true
})
export class ChatMarkdownPipe implements PipeTransform {
  private static readonly codeBlockToken = 'CHATCODEBLOCK';

  constructor(private sanitizer: DomSanitizer) {}

  transform(value?: string | null): SafeHtml {
    if (!value) {
      return '';
    }

    const codeBlocks: string[] = [];
    const token = ChatMarkdownPipe.codeBlockToken;

    const withPlaceholders = this.escape(value).replace(
      /```[^\n]*\n?([\s\S]*?)```/g,
      (_, code: string) => {
        codeBlocks.push(`<pre><code>${code.trimEnd()}</code></pre>`);
        return `\n${token}${codeBlocks.length - 1}\n`;
      }
    );

    return this.sanitizer.bypassSecurityTrustHtml(
      this.render(withPlaceholders.split('\n'), codeBlocks)
    );
  }

  private render(lines: string[], codeBlocks: string[]): string {
    const html: string[] = [];
    const lists: OpenList[] = [];
    let paragraph: string[] = [];
    /** Running count of task-list items, used to address each checkbox back to the source. */
    let taskIndex = 0;

    const closeParagraph = () => {
      if (paragraph.length) {
        html.push(`<p>${paragraph.join('<br>')}</p>`);
        paragraph = [];
      }
    };

    const closeList = () => {
      const list = lists.pop()!;
      if (list.itemOpen) {
        html.push('</li>');
      }
      html.push(`</${list.tag}>`);
    };

    const closeListsTo = (indent: number) => {
      while (lists.length && lists[lists.length - 1].indent > indent) {
        closeList();
      }
    };

    const closeAllLists = () => {
      while (lists.length) {
        closeList();
      }
    };

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      const trimmed = line.trim();

      if (!trimmed) {
        closeParagraph();
        continue;
      }

      // A collapsible block. Everything is HTML-escaped up front, so these are the escaped
      // forms; only this exact, attribute-free shape is turned back into real tags, which
      // keeps arbitrary HTML in imported or AI-written text inert.
      if (trimmed === '&lt;details&gt;') {
        closeParagraph();
        closeAllLists();
        html.push('<details>');
        continue;
      }
      if (trimmed === '&lt;/details&gt;') {
        closeParagraph();
        closeAllLists();
        html.push('</details>');
        continue;
      }
      const summary = /^&lt;summary&gt;(.*)&lt;\/summary&gt;$/.exec(trimmed);
      if (summary) {
        closeParagraph();
        closeAllLists();
        html.push(`<summary>${this.renderInline(summary[1])}</summary>`);
        continue;
      }

      const placeholder = new RegExp(`^${ChatMarkdownPipe.codeBlockToken}(\\d+)$`).exec(trimmed);
      if (placeholder) {
        closeParagraph();
        closeAllLists();
        html.push(codeBlocks[Number(placeholder[1])] ?? '');
        continue;
      }

      const tableLines = this.collectTable(lines, i);
      if (tableLines) {
        closeParagraph();
        closeAllLists();
        html.push(this.renderTable(tableLines));
        i += tableLines.length - 1;
        continue;
      }

      const heading = /^(#{1,6})\s+(.*)$/.exec(trimmed);
      if (heading) {
        closeParagraph();
        closeAllLists();
        const level = Math.min(Math.max(heading[1].length, 3), 6);
        html.push(`<h${level}>${this.renderInline(heading[2])}</h${level}>`);
        continue;
      }

      const item = /^(\s*)(?:([-*+])|(\d+)[.)])\s+(.*)$/.exec(line);
      if (item) {
        closeParagraph();

        let indent = item[1].replace(/\t/g, '  ').length;
        const tag: 'ul' | 'ol' = item[2] ? 'ul' : 'ol';
        const enclosingOrdered = [...lists].reverse().find(list => list.tag === 'ol');

        if (enclosingOrdered && tag === 'ul' && indent <= enclosingOrdered.indent) {
          indent = enclosingOrdered.indent + 1;
        }

        const reusable = [...lists]
          .reverse()
          .find(list => list.tag === tag && list.indent <= indent);

        if (reusable) {
          while (lists.length && lists[lists.length - 1] !== reusable) {
            closeList();
          }

          if (reusable.itemOpen) {
            html.push('</li>');
            reusable.itemOpen = false;
          }
        } else {
          closeListsTo(indent);

          const parent = lists[lists.length - 1];
          if (parent && !parent.itemOpen) {
            html.push('<li>');
            parent.itemOpen = true;
          }

          lists.push({ tag, indent, itemOpen: false });
          html.push(`<${tag}>`);
        }

        // Task list item: "- [ ] text" / "- [x] text". Each checkbox is numbered by its
        // position among the task items in the body (data-task-index) rather than by line
        // number: a fenced code block collapses to a single placeholder line before rendering,
        // so line indices here do not match the original markdown. Counting task items is
        // stable because the host re-scans the body with the same ordering.
        // The checkbox is inert on its own — the host component opts in by handling clicks.
        const task = /^\[([ xX])\]\s+(.*)$/.exec(item[4]);
        if (task) {
          const checked = task[1].toLowerCase() === 'x';
          html.push(
            `<li class="task-item">` +
            `<input type="checkbox" class="task-checkbox" data-task-index="${taskIndex++}"` +
            `${checked ? ' checked' : ''}>` +
            `<span class="task-text${checked ? ' done' : ''}">${this.renderInline(task[2])}</span>`
          );
          lists[lists.length - 1].itemOpen = true;
          continue;
        }

        html.push(`<li>${this.renderInline(item[4])}`);
        lists[lists.length - 1].itemOpen = true;
        continue;
      }

      if (lists.length) {
        closeAllLists();
      }

      paragraph.push(this.renderInline(trimmed));
    }

    closeParagraph();
    closeAllLists();

    return html.join('');
  }

  private collectTable(lines: string[], start: number): string[] | null {
    const isRow = (line?: string) => !!line && /^\s*\|.*\|\s*$/.test(line);

    if (!isRow(lines[start]) || !isRow(lines[start + 1])) {
      return null;
    }

    if (!/^\s*\|[\s:|-]+\|\s*$/.test(lines[start + 1])) {
      return null;
    }

    const collected: string[] = [];
    for (let i = start; i < lines.length && isRow(lines[i]); i++) {
      collected.push(lines[i]);
    }

    return collected;
  }

  private renderTable(lines: string[]): string {
    const rows = lines
      .filter(line => !/^\s*\|[\s:|-]+\|\s*$/.test(line))
      .map(line =>
        line
          .trim()
          .replace(/^\||\|$/g, '')
          .split('|')
          .map(cell => this.renderInline(cell.trim()))
      );

    if (!rows.length) {
      return '';
    }

    const head = `<tr>${rows[0].map(cell => `<th>${cell}</th>`).join('')}</tr>`;
    const body = rows
      .slice(1)
      .map(row => `<tr>${row.map(cell => `<td>${cell}</td>`).join('')}</tr>`)
      .join('');

    return `<div class="chat-table-scroll"><table><thead>${head}</thead><tbody>${body}</tbody></table></div>`;
  }

  private renderInline(text: string): string {
    return text
      .replace(/`([^`]+)`/g, '<code>$1</code>')
      .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
      .replace(/(^|[^*])\*([^*\n]+)\*/g, '$1<em>$2</em>')
      .replace(
        /\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g,
        '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>'
      );
  }

  private escape(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');
  }
}
