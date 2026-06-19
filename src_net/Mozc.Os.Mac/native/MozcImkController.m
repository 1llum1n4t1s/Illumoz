/* macOS IMK 極薄 native stub。IMKInputController サブクラスが NSEvent を C#
 * (NativeAOT 共有ライブラリ Mozc.Os.Mac)のエクスポート関数へ転送するだけ。
 * 変換ロジック・IPC は全て C#(ImkBridge → ImkController → ImeClient → mozc_server)。
 * 実機 macOS で Cocoa/InputMethodKit を使ってコンパイルする(この sandbox では未コンパイル)。
 */
#import <Cocoa/Cocoa.h>
#import <InputMethodKit/InputMethodKit.h>
#include <stdint.h>

/* C# (NativeAOT) エクスポート。 */
extern int mozc_imk_process_key(uint16_t keyCode, const char *charsUtf8, int charsLen, uint32_t modifiers);
extern int mozc_imk_get_preedit(char *buf, int cap);
extern int mozc_imk_get_commit(char *buf, int cap);
extern int mozc_imk_get_candidates(char *buf, int cap); /* 改行区切り候補列 */

@interface MozcImkInputController : IMKInputController
@end

@implementation MozcImkInputController

- (BOOL)handleEvent:(NSEvent *)event client:(id)sender {
    if (event.type != NSEventTypeKeyDown) {
        return NO;
    }
    /* ショートカット照合のため、修飾を適用済みの characters ではなく
     * charactersIgnoringModifiers(Shift 以外の修飾を無視した素のキー識別)を渡す。
     * 例: Ctrl+h は Ctrl+U+0008 ではなく 'h' として送られ、サーバが "Ctrl h" を一致できる。 */
    NSString *keyChars = event.charactersIgnoringModifiers ?: event.characters;
    const char *chars = keyChars ? keyChars.UTF8String : "";
    int consumed = mozc_imk_process_key((uint16_t)event.keyCode,
                                        chars, (int)strlen(chars),
                                        (uint32_t)event.modifierFlags);

    char commit[1024];
    int n = mozc_imk_get_commit(commit, sizeof(commit));
    if (n > 0 && n < (int)sizeof(commit)) {
        commit[n] = '\0';
        [sender insertText:[NSString stringWithUTF8String:commit]
          replacementRange:NSMakeRange(NSNotFound, NSNotFound)];
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

    /* 候補列(改行区切り)を IMKCandidates へ供給。空なら隠す。 */
    char cands[4096];
    int c = mozc_imk_get_candidates(cands, sizeof(cands));
    IMKCandidates *panel = [self candidates];
    if (panel) {
        if (c > 0 && c < (int)sizeof(cands)) {
            cands[c] = '\0';
            [panel updateCandidates];
            [panel show:kIMKLocateCandidatesBelowHint];
        } else {
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
