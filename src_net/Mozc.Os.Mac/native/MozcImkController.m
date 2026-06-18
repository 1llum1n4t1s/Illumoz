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
    const char *chars = event.characters ? event.characters.UTF8String : "";
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
        [sender setMarkedText:s selectionRange:NSMakeRange(p, 0)
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
