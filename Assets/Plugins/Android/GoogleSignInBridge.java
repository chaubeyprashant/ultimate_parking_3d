package com.ultimateparking.auth;

import android.app.Activity;
import android.content.Context;
import android.os.CancellationSignal;

import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.util.concurrent.Executor;
import java.util.concurrent.Executors;

/**
 * "Sign in with Google" for Unity via AndroidX Credential Manager. Shows Google's account sheet and returns a Google
 * ID token for Firebase Auth (GoogleAuthProvider). The web (server) client ID comes from the
 * default_web_client_id string that the Firebase Unity SDK generates from google-services.json.
 */
public final class GoogleSignInBridge {
    /** Implemented in C# (AndroidJavaProxy). Called on a background thread. */
    public interface Callback {
        void onSuccess(String idToken, String displayName, String email);
        void onError(String code, String message);
    }

    private static final Executor EXECUTOR = Executors.newSingleThreadExecutor();

    private GoogleSignInBridge() { }

    /** The OAuth web client ID from google-services.json, or "" if missing. */
    public static String webClientId(Context context) {
        int id = context.getResources().getIdentifier("default_web_client_id", "string", context.getPackageName());
        return id == 0 ? "" : context.getString(id);
    }

    public static void signIn(final Activity activity, final Callback callback) {
        final String clientId = webClientId(activity);
        if (clientId.isEmpty()) {
            callback.onError("config", "default_web_client_id missing (google-services.json not processed)");
            return;
        }
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                try {
                    GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(clientId).build();
                    GetCredentialRequest request = new GetCredentialRequest.Builder().addCredentialOption(option).build();
                    CredentialManager.create(activity).getCredentialAsync(activity, request, new CancellationSignal(), EXECUTOR,
                        new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                            @Override public void onResult(GetCredentialResponse response) {
                                Credential c = response.getCredential();
                                if (c instanceof CustomCredential && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(c.getType())) {
                                    try {
                                        GoogleIdTokenCredential g = GoogleIdTokenCredential.createFrom(((CustomCredential) c).getData());
                                        callback.onSuccess(g.getIdToken(), g.getDisplayName() == null ? "" : g.getDisplayName(), g.getId());
                                    } catch (Exception e) {
                                        callback.onError("parse", String.valueOf(e.getMessage()));
                                    }
                                } else {
                                    callback.onError("type", "unexpected credential type " + c.getType());
                                }
                            }

                            @Override public void onError(GetCredentialException e) {
                                String code = e instanceof GetCredentialCancellationException ? "cancelled"
                                    : e instanceof NoCredentialException ? "no_account" : "error";
                                callback.onError(code, String.valueOf(e.getMessage()));
                            }
                        });
                } catch (Throwable t) {
                    callback.onError("exception", String.valueOf(t.getMessage()));
                }
            }
        });
    }

    /** Forget the chosen Google account (so the account sheet shows again next time). */
    public static void signOut(final Activity activity) {
        try {
            CredentialManager.create(activity).clearCredentialStateAsync(new ClearCredentialStateRequest(), new CancellationSignal(), EXECUTOR,
                new CredentialManagerCallback<Void, ClearCredentialException>() {
                    @Override public void onResult(Void result) { }
                    @Override public void onError(ClearCredentialException e) { }
                });
        } catch (Throwable ignored) { }
    }
}
