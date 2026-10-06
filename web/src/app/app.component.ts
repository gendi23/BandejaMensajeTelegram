import { CommonModule, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

interface Conversation {
  chatId: number;
  displayName: string;
  username: string | null;
  unreadCount: number;
  lastMessage: string | null;
  lastMessageAt: string | null;
}

interface ChatMessage {
  id: string;
  telegramMessageId: number | null;
  text: string;
  isOutgoing: boolean;
  isRead: boolean;
  createdAt: string;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent implements OnInit, OnDestroy {
  readonly conversations = signal<Conversation[]>([]);
  readonly messages = signal<ChatMessage[]>([]);
  readonly selected = signal<Conversation | null>(null);
  readonly searchTerm = signal('');
  readonly sending = signal(false);
  readonly connectionState = signal<'connecting' | 'connected' | 'disconnected'>('connecting');
  draft = '';
  private connection?: HubConnection;

  constructor(private readonly http: HttpClient) {}

  ngOnInit(): void {
    this.refreshConversations();
    this.connectRealtime();
  }

  ngOnDestroy(): void {
    void this.connection?.stop();
  }

  get filteredConversations(): Conversation[] {
    const query = this.searchTerm().trim().toLocaleLowerCase();
    if (!query) return this.conversations();
    return this.conversations().filter((conversation) =>
      `${conversation.displayName} ${conversation.username ?? ''} ${conversation.lastMessage ?? ''}`
        .toLocaleLowerCase()
        .includes(query),
    );
  }

  get totalUnread(): number {
    return this.conversations().reduce((total, conversation) => total + conversation.unreadCount, 0);
  }

  initials(conversation: Conversation): string {
    return conversation.displayName
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part.charAt(0))
      .join('')
      .toLocaleUpperCase();
  }

  openConversation(conversation: Conversation): void {
    this.selected.set(conversation);
    this.messages.set([]);
    this.refreshMessages(conversation.chatId);
    this.http.post<void>(`/api/conversations/${conversation.chatId}/read`, {}).subscribe({
      next: () => this.refreshConversations(),
    });
  }

  sendReply(event: Event): void {
    event.preventDefault();
    const conversation = this.selected();
    const text = this.draft.trim();
    if (!conversation || !text || this.sending()) return;

    this.sending.set(true);
    this.http.post<ChatMessage>(`/api/conversations/${conversation.chatId}/messages`, { text }).subscribe({
      next: () => {
        this.draft = '';
        this.sending.set(false);
        this.refreshMessages(conversation.chatId);
        this.refreshConversations();
      },
      error: () => this.sending.set(false),
    });
  }

  handleKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Enter' || event.shiftKey) return;
    event.preventDefault();
    this.sendReply(event);
  }

  private refreshConversations(): void {
    this.http.get<Conversation[]>('/api/conversations').subscribe({
      next: (conversations) => this.conversations.set(conversations),
    });
  }

  private refreshMessages(chatId: number): void {
    this.http.get<ChatMessage[]>(`/api/conversations/${chatId}/messages`).subscribe({
      next: (messages) => {
        if (this.selected()?.chatId === chatId) this.messages.set(messages);
      },
    });
  }

  private connectRealtime(): void {
    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/inbox')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('conversationChanged', (chatId: number) => {
      this.refreshConversations();
      if (this.selected()?.chatId === chatId) this.refreshMessages(chatId);
    });
    this.connection.onreconnecting(() => this.connectionState.set('connecting'));
    this.connection.onreconnected(() => this.connectionState.set('connected'));
    this.connection.onclose(() => this.connectionState.set('disconnected'));
    void this.connection.start()
      .then(() => this.connectionState.set('connected'))
      .catch(() => this.connectionState.set('disconnected'));
  }
}
