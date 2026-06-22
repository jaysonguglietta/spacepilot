import SwiftUI

@main
struct SpacePilotMacApp: App {
    @StateObject private var state = MacAppState()

    var body: some Scene {
        WindowGroup("SpacePilot") {
            RootView()
                .environmentObject(state)
                .frame(minWidth: 1080, minHeight: 700)
        }
        .commands {
            CommandGroup(replacing: .newItem) {}
        }
    }
}
