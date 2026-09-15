import { createSocialImage, SOCIAL_IMAGE_SIZE } from "./social-preview";

export const alt =
  "Pathdle — chart the hidden path through a constellation of Wikipedia articles";
export const size = SOCIAL_IMAGE_SIZE;
export const contentType = "image/png";

export default function TwitterImage() {
  return createSocialImage();
}
