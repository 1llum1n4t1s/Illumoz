/* macOS IMK 極薄 native stub。IMKInputController サブクラスが NSEvent を C#
 * (NativeAOT 共有ライブラリ Mozc.Os.Mac)のエクスポート関数へ転送するだけ。
 * 変換ロジック・IPC は全て C#(ImkBridge → ImkController → ImeClient → mozc_server)。
 * 実機 macOS で Cocoa/InputMethodKit を使ってコンパイルする(この sandbox では未コンパイル)。
 */
#import <Cocoa/Cocoa.h>
#import <InputMethodKit/InputMethodKit.h>
#include <stdint.h>
#include <stdlib.h>

/* C# (NativeAOT) エクスポート。 */
extern int mozc_imk_process_key(uint16_t keyCode, const char *charsUtf8, int charsLen, uint32_t modifiers);
extern int mozc_imk_get_preedit(char *buf, int cap);
extern int mozc_imk_get_commit(char *buf, int cap);
extern int mozc_imk_get_candidates(char *buf, int cap); /* 改行区切り候補列 */

@interface MozcImkInputController : IMKInputController {
    /* 直近の変換候補(IMKCandidates のデータソースとして返す)。 */
    NSArray<NSString *> *_candidateStrings;
}
@end

@implementation MozcImkInputController

/* IMKCandidates のデータソース。updateCandidates 時に IMK がここを呼んで候補配列を取得する。
 * これを実装しないと updateCandidates が空のソースを参照し、候補ウィンドウに何も出ない。 */
- (NSArray *)candidates:(id)sender {
    (void)sender;
    return _candidateStrings ?: @[];
}

- (BOOL)handleEvent:(NSEvent *)event client:(id)sender {
    if (event.type != NSEventTypeKeyDown) {
        return NO;
    }
    /* Ctrl/Cmd/Option を伴うキーはショートカット照合のため charactersIgnoringModifiers
     * (Shift 以外の修飾を無視した素のキー識別)を渡す。例: Ctrl+h は Ctrl+U+0008 ではなく
     * 'h' として送られサーバが "Ctrl h" を一致できる。
     * 一方それら command 系修飾が無い印字入力(素 or Shift のみ)は characters を使う。
     * charactersIgnoringModifiers だと Shift+1 が '1'、Shift+a が 'a' になり、サーバの印字
     * フォールバックが非シフト文字を挿入してしまうため('!' や 'A' を取りこぼす)。 */
    NSEventModifierFlags commandMods = event.modifierFlags
        & (NSEventModifierFlagControl | NSEventModifierFlagCommand | NSEventModifierFlagOption);
    NSString *keyChars = commandMods != 0
        ? (event.charactersIgnoringModifiers ?: event.characters)
        : (event.characters ?: event.charactersIgnoringModifiers);
    const char *chars = keyChars ? keyChars.UTF8String : "";
    int consumed = mozc_imk_process_key((uint16_t)event.keyCode,
                                        chars, (int)strlen(chars),
                                        (uint32_t)event.modifierFlags);

    /* commit 文字列を確定。固定バッファに収まらない長文(辞書の長語句や長い
       TEXT_INPUT)は必要長で動的確保して取りこぼさない(IBus 側と同様)。 */
    char commit_buf[1024];
    int n = mozc_imk_get_commit(commit_buf, sizeof(commit_buf));
    if (n > 0) {
        char *commit = commit_buf;
        char *heap = NULL;
        if (n > (int)sizeof(commit_buf) - 1) {
            heap = (char *)malloc((size_t)n + 1);
            if (heap != NULL && mozc_imk_get_commit(heap, n + 1) == n) {
                commit = heap;
            } else {
                commit = NULL;
            }
        }
        if (commit != NULL) {
            commit[n] = '\0';
            [sender insertText:[NSString stringWithUTF8String:commit]
              replacementRange:NSMakeRange(NSNotFound, NSNotFound)];
        }
        free(heap);
    }

    char preedit[1024];
    int p = mozc_imk_get_preedit(preedit, sizeof(preedit));
    if (p >= 0 && p < (int)sizeof(preedit)) {
        preedit[p] = '\0';
        NSString *s = [NSString stringWithUTF8String:preedit];
        /* selectionRange は NSString(UTF-16)インデックス。UTF-8 バイト数 p ではなく
         * s.length を使う(かな等で 1 文字=複数 UTF-8 バイトのとき位置がずれる)。 */
        [sender setMarkedText:s selectionRange:NSMakeRange(s.length, 0)
             replacementRange:NSMakeRange(NSNotFound, NSNotFound)];
    }

    /* 候補列(改行区切り)を IMKCandidates へ供給。空なら隠す。
     * cands を改行で分割して _candidateStrings に格納し、candidates: データソース経由で
     * パネルへ渡す(updateCandidates だけでは新しい候補がソースに入らず空表示になる)。 */
    char cands[4096];
    int c = mozc_imk_get_candidates(cands, sizeof(cands));
    IMKCandidates *panel = [self candidates];
    if (panel) {
        if (c > 0 && c < (int)sizeof(cands)) {
            cands[c] = '\0';
            NSString *joined = [NSString stringWithUTF8String:cands];
            NSArray<NSString *> *list =
                [joined componentsSeparatedByString:@"\n"];
            /* 末尾の空要素(改行終端)を除去して空候補がパネルに混じらないようにする。 */
            NSMutableArray<NSString *> *filtered = [NSMutableArray arrayWithCapacity:list.count];
            for (NSString *item in list) {
                if (item.length > 0) {
                    [filtered addObject:item];
                }
            }
            _candidateStrings = filtered;
            if (filtered.count > 0) {
                [panel updateCandidates];
                [panel show:kIMKLocateCandidatesBelowHint];
            } else {
                [panel hide];
            }
        } else {
            _candidateStrings = @[];
            [panel hide];
        }
    }

    return consumed ? YES : NO;
}

@end

/* C# 側 controller(IPC トランスポート)初期化。 */
extern int mozc_imk_init(void);

/* 入力メソッドサーバのエントリポイント。
 * IMKServer を生成して接続名(Info.plist の InputMethodConnectionName)を待ち受け、
 * NSApplication のランループに入る。bundle として起動される。 */
int main(int argc, const char *argv[]) {
    (void)argc;
    (void)argv;
    @autoreleasepool {
        if (!mozc_imk_init()) {
            NSLog(@"mozc_imk_init failed; aborting");
            return 1;
        }
        NSString *connectionName =
            [[NSBundle mainBundle].infoDictionary objectForKey:@"InputMethodConnectionName"];
        if (connectionName == nil) {
            connectionName = @"Mozc_Connection";
        }
        IMKServer *server __attribute__((unused)) =
            [[IMKServer alloc] initWithName:connectionName
                           bundleIdentifier:[NSBundle mainBundle].bundleIdentifier];
        [[NSApplication sharedApplication] run];
    }
    return 0;
}
